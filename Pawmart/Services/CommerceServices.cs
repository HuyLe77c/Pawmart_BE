using Microsoft.EntityFrameworkCore;
using Pawmart.Data;
using Pawmart.Middleware;
using Pawmart.Models.Contracts.Catalog;
using Pawmart.Models.Contracts.Commerce;
using Pawmart.Models.Contracts.Orders;
using Pawmart.Models.Entities;
using Pawmart.Services.Integrations;

namespace Pawmart.Services;

public sealed class PromotionService(PawmartDbContext db)
{
    public async Task<IReadOnlyList<PromotionResponse>> ListActiveAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await db.Promotions.AsNoTracking().Where(x => x.IsActive && x.StartAt <= now && x.EndAt > now && (!x.UsageLimit.HasValue || x.UsedCount < x.UsageLimit.Value))
            .OrderBy(x => x.EndAt).Select(x => new PromotionResponse(x.PromotionId, x.Code, x.PromotionName, x.Description, x.DiscountType, x.DiscountValue, x.MinOrderAmount, x.MaxDiscountAmount, x.StartAt, x.EndAt, x.IsActive)).ToListAsync(ct);
    }

    public async Task<PagedResponse<PromotionResponse>> ListAdminAsync(int page, int pageSize, CancellationToken ct)
    {
        var q = db.Promotions.AsNoTracking().OrderByDescending(x => x.CreatedAt);
        var total = await q.CountAsync(ct); page = Math.Clamp(page, 1, 1000); pageSize = Math.Clamp(pageSize, 1, 100);
        var values = await q.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new PromotionResponse(x.PromotionId, x.Code, x.PromotionName, x.Description, x.DiscountType, x.DiscountValue, x.MinOrderAmount, x.MaxDiscountAmount, x.StartAt, x.EndAt, x.IsActive)).ToListAsync(ct);
        return new PagedResponse<PromotionResponse>(values, page, pageSize, total);
    }

    public async Task<PromotionResponse> CreateAsync(PromotionWriteRequest request, CancellationToken ct)
    {
        Validate(request);
        var promotion = new Promotion { CreatedAt = DateTime.UtcNow, UsedCount = 0, IsActive = true };
        Apply(promotion, request);
        db.Promotions.Add(promotion); await db.SaveChangesAsync(ct);
        return ToResponse(promotion);
    }

    public async Task<PromotionResponse> UpdateAsync(int id, PromotionWriteRequest request, CancellationToken ct)
    {
        Validate(request);
        var promotion = await db.Promotions.SingleOrDefaultAsync(x => x.PromotionId == id, ct)
            ?? throw new ApiException(404, "Promotion was not found.", "Not found");
        Apply(promotion, request); promotion.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); return ToResponse(promotion);
    }

    public async Task SetActiveAsync(int id, bool active, CancellationToken ct)
    {
        var promotion = await db.Promotions.SingleOrDefaultAsync(x => x.PromotionId == id, ct)
            ?? throw new ApiException(404, "Promotion was not found.", "Not found");
        promotion.IsActive = active; promotion.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
    }

    private static void Validate(PromotionWriteRequest request)
    {
        if (request.EndAt <= request.StartAt) throw new ApiException(400, "EndAt must be later than StartAt.", "Invalid promotion dates");
        if (request.DiscountType == "PERCENT" && request.DiscountValue > 100) throw new ApiException(400, "Percentage discount cannot exceed 100.", "Invalid discount");
    }
    private static void Apply(Promotion p, PromotionWriteRequest r)
    {
        p.Code = r.Code.Trim().ToUpperInvariant(); p.PromotionName = r.PromotionName.Trim(); p.Description = r.Description;
        p.DiscountType = r.DiscountType; p.DiscountValue = r.DiscountValue; p.MinOrderAmount = r.MinOrderAmount; p.MaxDiscountAmount = r.MaxDiscountAmount;
        p.StartAt = r.StartAt.UtcDateTime; p.EndAt = r.EndAt.UtcDateTime; p.UsageLimit = r.UsageLimit;
    }
    private static PromotionResponse ToResponse(Promotion p) => new(p.PromotionId, p.Code, p.PromotionName, p.Description, p.DiscountType, p.DiscountValue, p.MinOrderAmount, p.MaxDiscountAmount, p.StartAt, p.EndAt, p.IsActive);
}

public sealed class StoreService(PawmartDbContext db)
{
    public async Task<IReadOnlyList<StoreResponse>> ListActiveAsync(CancellationToken ct) =>
        await db.Stores.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.StoreType).ThenBy(x => x.StoreName)
            .Select(x => new StoreResponse(x.StoreId, x.StoreName, x.StoreType, x.Address, x.Latitude, x.Longitude, x.PhoneNumber, x.IsActive)).ToListAsync(ct);

    public async Task<IReadOnlyList<StoreResponse>> ListForAdminAsync(CancellationToken ct) =>
        await db.Stores.AsNoTracking().OrderBy(x => x.StoreId)
            .Select(x => new StoreResponse(x.StoreId, x.StoreName, x.StoreType, x.Address, x.Latitude, x.Longitude, x.PhoneNumber, x.IsActive)).ToListAsync(ct);

    public async Task<StoreResponse> CreateAsync(StoreWriteRequest request, CancellationToken ct)
    {
        var store = new Store { CreatedAt = DateTime.UtcNow, IsActive = true }; Apply(store, request);
        db.Stores.Add(store); await db.SaveChangesAsync(ct); return ToResponse(store);
    }
    public async Task<StoreResponse> UpdateAsync(int id, StoreWriteRequest request, CancellationToken ct)
    {
        var store = await db.Stores.SingleOrDefaultAsync(x => x.StoreId == id, ct) ?? throw new ApiException(404, "Store was not found.", "Not found");
        Apply(store, request); store.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return ToResponse(store);
    }
    public async Task SetActiveAsync(int id, bool active, CancellationToken ct)
    {
        var store = await db.Stores.SingleOrDefaultAsync(x => x.StoreId == id, ct) ?? throw new ApiException(404, "Store was not found.", "Not found");
        store.IsActive = active; store.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
    }
    private static void Apply(Store s, StoreWriteRequest r)
    {
        s.StoreName = r.StoreName.Trim(); s.StoreType = r.StoreType; s.Address = r.Address.Trim(); s.Latitude = r.Latitude; s.Longitude = r.Longitude;
        s.PhoneNumber = string.IsNullOrWhiteSpace(r.PhoneNumber) ? null : r.PhoneNumber.Trim();
    }
    private static StoreResponse ToResponse(Store s) => new(s.StoreId, s.StoreName, s.StoreType, s.Address, s.Latitude, s.Longitude, s.PhoneNumber, s.IsActive);
}

public sealed class NotificationService(PawmartDbContext db, IPushNotificationSender pushSender, ILogger<NotificationService> logger)
{
    public async Task<PagedResponse<NotificationResponse>> ListAsync(string userId, int page, int pageSize, CancellationToken ct)
    {
        var q = db.Notifications.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt);
        var total = await q.CountAsync(ct); page = Math.Clamp(page, 1, 1000); pageSize = Math.Clamp(pageSize, 1, 100);
        var values = await q.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new NotificationResponse(x.NotificationId, x.Title, x.Message, x.NotificationType, x.ReferenceId, x.IsRead, x.CreatedAt)).ToListAsync(ct);
        return new PagedResponse<NotificationResponse>(values, page, pageSize, total);
    }

    public async Task MarkReadAsync(string userId, int id, CancellationToken ct)
    {
        var item = await db.Notifications.SingleOrDefaultAsync(x => x.UserId == userId && x.NotificationId == id, ct)
            ?? throw new ApiException(404, "Notification was not found.", "Not found");
        item.IsRead = true; await db.SaveChangesAsync(ct);
    }

    public async Task UpdateTokenAsync(string userId, string? token, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.UserId == userId && x.IsActive, ct)
            ?? throw new ApiException(404, "User was not found.", "Not found");
        user.FcmToken = string.IsNullOrWhiteSpace(token) ? null : token.Trim();
        user.FcmTokenUpdatedAt = user.FcmToken is null ? null : DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<NotificationBroadcastResponse> BroadcastAsync(NotificationCreateRequest request, CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().Where(x => x.IsActive && x.Role == "CUSTOMER").Select(x => new { x.UserId, x.FcmToken }).ToListAsync(ct);
        db.Notifications.AddRange(users.Select(user => new Notification
        {
            UserId = user.UserId, Title = request.Title.Trim(), Message = request.Message.Trim(),
            NotificationType = request.NotificationType, ReferenceId = request.ReferenceId, CreatedAt = DateTime.UtcNow
        }));
        await db.SaveChangesAsync(ct);
        PushDeliveryResult delivery;
        try
        {
            var tokens = users.Where(x => !string.IsNullOrWhiteSpace(x.FcmToken)).Select(x => x.FcmToken!).Distinct().ToArray();
            delivery = await pushSender.SendAsync(tokens, request.Title.Trim(), request.Message.Trim(), ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Push delivery failed after persisting admin notification broadcast.");
            delivery = new PushDeliveryResult(true, 0, "Notification was saved, but push delivery failed.");
        }
        return new NotificationBroadcastResponse(users.Count, true, delivery.Available, delivery.SentCount, delivery.Detail);
    }
}

public sealed class AdminReportService(PawmartDbContext db)
{
    public async Task<RevenueReport> GetRevenueAsync(RevenueReportQuery query, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var now = DateTime.UtcNow;
        if (query.From.HasValue != query.To.HasValue)
            throw new ApiException(400, "Both from and to dates are required when filtering by period.", "Invalid date range");
        var start = query.From?.UtcDateTime ?? new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = query.To?.UtcDateTime ?? now;
        if (start >= end || end - start > TimeSpan.FromDays(366))
            throw new ApiException(400, "The report period must be positive and no longer than 366 days.", "Invalid date range");
        var orders = db.Orders.AsNoTracking().Where(x => x.OrderStatus == "DELIVERED");
        var periodOrders = orders.Where(x => x.DeliveredAt >= start && x.DeliveredAt < end);
        var periodRevenue = await periodOrders.SumAsync(x => (decimal?)x.GrandTotal, ct) ?? 0m;
        var todayOrders = orders.Where(x => x.DeliveredAt >= today && x.DeliveredAt < today.AddDays(1));
        var todayRevenue = await todayOrders.SumAsync(x => (decimal?)x.GrandTotal, ct) ?? 0m;
        var count = await todayOrders.CountAsync(ct);
        return new RevenueReport(start, end, periodRevenue, todayRevenue, count);
    }

    public async Task<IReadOnlyList<OrderStatusCount>> GetOrderCountsAsync(CancellationToken ct)
    {
        var counts = await db.Orders.AsNoTracking()
            .GroupBy(x => x.OrderStatus)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .OrderBy(row => row.Status)
            .ToListAsync(ct);
        return counts.Select(row => new OrderStatusCount(row.Status, row.Count)).ToList();
    }

    public async Task<IReadOnlyList<LowStockProduct>> GetLowStockAsync(CancellationToken ct) =>
        await db.Products.AsNoTracking().Where(x => x.IsActive && x.StockQuantity <= x.LowStockThreshold)
            .OrderBy(x => x.StockQuantity).Select(x => new LowStockProduct(x.ProductId, x.ProductName, x.StockQuantity, x.LowStockThreshold,
                x.StockQuantity == 0 ? "OUT_OF_STOCK" : "LOW_STOCK")).ToListAsync(ct);
}
