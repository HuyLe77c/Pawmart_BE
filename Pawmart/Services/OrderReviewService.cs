using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pawmart.Data;
using Pawmart.Middleware;
using Pawmart.Models.Contracts.Catalog;
using Pawmart.Models.Contracts.Orders;
using Pawmart.Models.Entities;

namespace Pawmart.Services;

public sealed class OrderReviewService(PawmartDbContext db)
{
    public async Task<OrderReviewResponse> CreateAsync(string userId, string orderId,
        CreateOrderReviewRequest request, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, ct)
            ?? throw new ApiException(404, "Order was not found.", "Not found");
        if (order.OrderStatus != "DELIVERED")
            throw new ApiException(409, "You can review this order after delivery.", "Order not delivered");
        var paid = await db.Payments.AsNoTracking().AnyAsync(
            x => x.OrderId == orderId && x.PaymentStatus == "SUCCESS", ct);
        if (!paid)
            throw new ApiException(409, "You can review this order after payment is confirmed.", "Payment pending");
        if (await db.OrderReviews.AsNoTracking().AnyAsync(x => x.OrderId == orderId, ct))
            throw new ApiException(409, "This order has already been reviewed.", "Already reviewed");

        var comment = request.Comment?.Trim();
        if (comment?.Length == 1)
            throw new ApiException(400, "Enter at least two characters or leave the comment empty.", "Invalid comment");
        var fullName = await db.Users.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => x.FullName).SingleAsync(ct);
        var names = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var author = names.Length == 0 ? "Khách hàng" : names[^1];
        var review = new OrderReview
        {
            OrderId = orderId,
            UserId = userId,
            AuthorName = author[..Math.Min(author.Length, 60)],
            Rating = request.Rating,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment,
            SharePublicly = request.SharePublicly,
            CreatedAt = DateTime.UtcNow
        };
        db.OrderReviews.Add(review);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ApiException(409, "This order has already been reviewed.", "Already reviewed");
        }
        return Map(review);
    }

    public async Task<PagedResponse<OrderReviewResponse>> ListForAdminAsync(int page, int pageSize, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 1000);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await db.OrderReviews.AsNoTracking().CountAsync(ct);
        var rows = await db.OrderReviews.AsNoTracking().OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.ReviewId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResponse<OrderReviewResponse>(rows.Select(Map).ToArray(), page, pageSize, total);
    }

    public async Task<PublicOrderReviewsResponse> ListPublicAsync(CancellationToken ct)
    {
        var query = db.OrderReviews.AsNoTracking().Where(x => x.SharePublicly && x.IsVisible);
        var total = await query.CountAsync(ct);
        var average = total == 0 ? 0m : Math.Round(await query.AverageAsync(x => (decimal)x.Rating, ct), 1);
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.ReviewId)
            .Take(12).Select(x => new PublicOrderReviewResponse(x.AuthorName, x.Rating,
                x.Comment, x.ShopReply, x.CreatedAt)).ToListAsync(ct);
        return new PublicOrderReviewsResponse(total, average, rows);
    }

    public async Task<OrderReviewResponse> ReplyAsync(int reviewId, ReplyOrderReviewRequest request, CancellationToken ct)
    {
        var review = await db.OrderReviews.SingleOrDefaultAsync(x => x.ReviewId == reviewId, ct)
            ?? throw new ApiException(404, "Review was not found.", "Not found");
        review.ShopReply = string.IsNullOrWhiteSpace(request.Reply) ? null : request.Reply.Trim();
        review.RepliedAt = review.ShopReply is null ? null : DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(review);
    }

    public async Task<OrderReviewResponse> SetVisibilityAsync(int reviewId,
        SetOrderReviewVisibilityRequest request, CancellationToken ct)
    {
        var review = await db.OrderReviews.SingleOrDefaultAsync(x => x.ReviewId == reviewId, ct)
            ?? throw new ApiException(404, "Review was not found.", "Not found");
        review.IsVisible = request.IsVisible;
        await db.SaveChangesAsync(ct);
        return Map(review);
    }

    public static OrderReviewResponse Map(OrderReview review) => new(review.ReviewId,
        review.OrderId, review.AuthorName, review.Rating, review.Comment,
        review.SharePublicly, review.IsVisible, review.ShopReply, review.CreatedAt, review.RepliedAt);
}
