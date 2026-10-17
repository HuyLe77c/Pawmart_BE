using System.Data;
using Microsoft.EntityFrameworkCore;
using Pawmart.Data;
using Pawmart.Middleware;
using Pawmart.Models.Contracts.Catalog;
using Pawmart.Models.Contracts.Orders;
using Pawmart.Models.Entities;

namespace Pawmart.Services;

public sealed class OrderService(PawmartDbContext db)
{
    public async Task<PagedResponse<OrderListItemResponse>> ListForCustomerAsync(string userId, OrderListQuery query, CancellationToken ct)
    {
        var orders = db.Orders.AsNoTracking().Where(x => x.UserId == userId);
        if (!string.IsNullOrWhiteSpace(query.Status)) orders = orders.Where(x => x.OrderStatus == query.Status);
        return await PageAsync(orders, query.Page, query.PageSize, ct);
    }

    public async Task<OrderDetailResponse> GetForCustomerAsync(string userId, string orderId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId && x.OrderId == orderId, ct)
            ?? throw new ApiException(404, "Order was not found.", "Not found");
        return await GetDetailAsync(order, ct);
    }

    public async Task<OrderDetailResponse> GetForAdminAsync(string orderId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == orderId, ct)
            ?? throw new ApiException(404, "Order was not found.", "Not found");
        return await GetDetailAsync(order, ct);
    }

    public async Task<PagedResponse<OrderListItemResponse>> ListForAdminAsync(OrderListQuery query, CancellationToken ct)
    {
        var orders = db.Orders.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Status)) orders = orders.Where(x => x.OrderStatus == query.Status);
        return await PageAsync(orders, query.Page, query.PageSize, ct);
    }

    public async Task CancelAsync(string userId, string orderId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await db.Orders.SingleOrDefaultAsync(x => x.UserId == userId && x.OrderId == orderId, ct)
            ?? throw new ApiException(404, "Order was not found.", "Not found");
        if (order.OrderStatus is not ("PENDING" or "CONFIRMED"))
            throw new ApiException(409, "This order can no longer be cancelled.", "Invalid order state");

        var existingPayment = await db.Payments.SingleOrDefaultAsync(x => x.OrderId == orderId, ct);
        if (existingPayment?.PaymentStatus == "SUCCESS" && order.PaymentMethod != "COD")
            throw new ApiException(409, "A successful online payment requires a refund workflow before cancellation.", "Refund required");

        await RestoreStockAsync(orderId, ct);
        await RestorePromotionUsageAsync(order.PromotionId, ct);
        order.OrderStatus = "CANCELLED";
        order.CancelledAt = DateTime.UtcNow;
        order.UpdatedAt = order.CancelledAt;
        var payment = existingPayment;
        if (payment is not null && payment.PaymentStatus == "PENDING")
        {
            payment.PaymentStatus = "CANCELLED";
            payment.UpdatedAt = order.CancelledAt;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task TransitionStatusAsync(string orderId, string targetStatus, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await db.Orders.SingleOrDefaultAsync(x => x.OrderId == orderId, ct)
            ?? throw new ApiException(404, "Order was not found.", "Not found");
        var allowed = order.OrderStatus switch
        {
            "PENDING" => targetStatus is "CONFIRMED" or "CANCELLED",
            "CONFIRMED" => targetStatus is "SHIPPING" or "CANCELLED",
            "SHIPPING" => targetStatus == "DELIVERED",
            _ => false
        };
        if (!allowed) throw new ApiException(409, $"Transition {order.OrderStatus} -> {targetStatus} is not allowed.", "Invalid order transition");

        var now = DateTime.UtcNow;
        if (targetStatus == "CONFIRMED" && order.PaymentMethod != "COD")
        {
            var onlinePayment = await db.Payments.SingleOrDefaultAsync(x => x.OrderId == orderId, ct);
            if (onlinePayment?.PaymentStatus != "SUCCESS")
                throw new ApiException(409, "Online payment must be confirmed before the order can be accepted.", "Payment not completed");
        }
        if (targetStatus == "CANCELLED")
        {
            var existingPayment = await db.Payments.SingleOrDefaultAsync(x => x.OrderId == orderId, ct);
            if (existingPayment?.PaymentStatus == "SUCCESS" && order.PaymentMethod != "COD")
                throw new ApiException(409, "A successful online payment requires a refund workflow before cancellation.", "Refund required");
            await RestoreStockAsync(orderId, ct);
            await RestorePromotionUsageAsync(order.PromotionId, ct);
            order.CancelledAt = now;
            if (existingPayment is not null && existingPayment.PaymentStatus == "PENDING")
            {
                existingPayment.PaymentStatus = "CANCELLED";
                existingPayment.UpdatedAt = now;
            }
        }
        else if (targetStatus == "CONFIRMED") order.ConfirmedAt = now;
        else if (targetStatus == "SHIPPING") order.ShippedAt = now;
        else if (targetStatus == "DELIVERED")
        {
            order.DeliveredAt = now;
            if (order.PaymentMethod == "COD")
            {
                var payment = await db.Payments.SingleOrDefaultAsync(x => x.OrderId == orderId, ct);
                if (payment is not null)
                {
                    payment.PaymentStatus = "SUCCESS";
                    payment.PaidAt = now;
                    payment.UpdatedAt = now;
                }
            }
        }
        order.OrderStatus = targetStatus;
        order.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task RestoreStockAsync(string orderId, CancellationToken ct)
    {
        var items = await db.OrderItems.AsNoTracking().Where(x => x.OrderId == orderId).ToListAsync(ct);
        foreach (var item in items)
        {
            await db.Products.Where(x => x.ProductId == item.ProductId)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.StockQuantity, x => x.StockQuantity + item.Quantity).SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
        }
    }

    private async Task RestorePromotionUsageAsync(int? promotionId, CancellationToken ct)
    {
        if (!promotionId.HasValue) return;
        await db.Promotions.Where(x => x.PromotionId == promotionId.Value && x.UsedCount > 0)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.UsedCount, x => x.UsedCount - 1).SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
    }

    private async Task<OrderDetailResponse> GetDetailAsync(Order order, CancellationToken ct)
    {
        var items = await (from line in db.OrderItems.AsNoTracking()
                           join product in db.Products.AsNoTracking() on line.ProductId equals product.ProductId
                           where line.OrderId == order.OrderId
                           orderby line.OrderItemId
                           select new OrderLineResponse(line.ProductId, product.ProductName, line.Quantity, line.UnitPrice, line.LineTotal)).ToListAsync(ct);
        var paymentStatus = await db.Payments.AsNoTracking().Where(x => x.OrderId == order.OrderId).Select(x => x.PaymentStatus).SingleOrDefaultAsync(ct) ?? "PENDING";
        var history = await db.OrderStatusHistory.AsNoTracking().Where(x => x.OrderId == order.OrderId).OrderBy(x => x.CreatedAt)
            .Select(x => new OrderStatusHistoryResponse(x.Status, x.Note, x.CreatedAt)).ToListAsync(ct);
        return new OrderDetailResponse(order.OrderId, order.SubtotalAmount, order.DiscountAmount, order.ShippingFee, order.GrandTotal,
            order.ShippingAddress, order.RecipientName, order.RecipientPhone, order.Latitude, order.Longitude, order.PaymentMethod,
            paymentStatus, order.OrderStatus, order.Note, order.CreatedAt, items, history);
    }

    private async Task<PagedResponse<OrderListItemResponse>> PageAsync(IQueryable<Order> query, int requestedPage, int requestedSize, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var page = Math.Clamp(requestedPage, 1, 1000);
        var size = Math.Clamp(requestedSize, 1, 100);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * size).Take(size)
            .Select(x => new { x.OrderId, x.GrandTotal, x.PaymentMethod, x.OrderStatus, x.CreatedAt }).ToListAsync(ct);
        var ids = rows.Select(x => x.OrderId).ToArray();
        var paymentStatuses = await db.Payments.AsNoTracking().Where(x => ids.Contains(x.OrderId)).ToDictionaryAsync(x => x.OrderId, x => x.PaymentStatus, ct);
        var values = rows.Select(x => new OrderListItemResponse(x.OrderId, x.GrandTotal, x.PaymentMethod, paymentStatuses.GetValueOrDefault(x.OrderId, "PENDING"), x.OrderStatus, x.CreatedAt)).ToArray();
        return new PagedResponse<OrderListItemResponse>(values, page, size, total);
    }
}
