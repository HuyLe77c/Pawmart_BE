using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pawmart.Data;
using Pawmart.Middleware;
using Pawmart.Models.Contracts.Orders;
using Pawmart.Models.Entities;

namespace Pawmart.Services;

public sealed class CheckoutService(PawmartDbContext db, IConfiguration configuration)
{
    public async Task<CheckoutQuote> GetQuoteAsync(string userId, string? promotionCode, CancellationToken ct)
    {
        var pricing = await PrepareAsync(userId, promotionCode, ct);
        return pricing.Quote;
    }

    public async Task<OrderSummary> CreateOrderAsync(string userId, CheckoutRequest request, CancellationToken ct)
    {
        if (request.Latitude.HasValue != request.Longitude.HasValue)
            throw new ApiException(400, "Latitude and longitude must be provided together.", "Invalid coordinates");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var pricing = await PrepareAsync(userId, request.PromotionCode, ct);
        var cart = pricing.Cart;
        var products = pricing.Products;
        var promotion = pricing.Promotion;
        var quote = pricing.Quote;
        if (request.ExpectedQuoteFingerprint is not null &&
            !string.Equals(request.ExpectedQuoteFingerprint, quote.Fingerprint, StringComparison.Ordinal))
            throw new ApiException(409, "Cart, prices, shipping fee, or promotion changed. Review the updated quote before ordering.", "Checkout quote changed");

        var subtotal = quote.Subtotal;
        var discount = quote.Discount;
        var flatFee = quote.ShippingFee;
        var grandTotal = quote.Total;
        var orderId = Guid.NewGuid().ToString("N");
        var order = new Order
        {
            OrderId = orderId,
            UserId = userId,
            SubtotalAmount = subtotal,
            DiscountAmount = discount,
            ShippingFee = flatFee,
            GrandTotal = grandTotal,
            PromotionId = promotion?.PromotionId,
            PromotionCodeSnapshot = promotion?.Code,
            ShippingAddress = request.ShippingAddress.Trim(),
            RecipientName = request.RecipientName.Trim(),
            RecipientPhone = request.RecipientPhone.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            PaymentMethod = request.PaymentMethod.ToUpperInvariant(),
            OrderStatus = "PENDING",
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in cart)
        {
            var affected = await db.Products.Where(p => p.ProductId == item.ProductId && p.IsActive && p.StockQuantity >= item.Quantity)
                .ExecuteUpdateAsync(update => update.SetProperty(p => p.StockQuantity, p => p.StockQuantity - item.Quantity).SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct);
            if (affected != 1) throw new ApiException(409, $"Stock changed for product {item.ProductId}; retry checkout.", "Insufficient stock");
        }

        if (promotion is not null)
        {
            var promoId = promotion.PromotionId;
            var updated = await db.Promotions.Where(p => p.PromotionId == promoId && p.IsActive && p.StartAt <= DateTime.UtcNow && p.EndAt > DateTime.UtcNow && (!p.UsageLimit.HasValue || p.UsedCount < p.UsageLimit.Value))
                .ExecuteUpdateAsync(update => update.SetProperty(p => p.UsedCount, p => p.UsedCount + 1).SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct);
            if (updated != 1) throw new ApiException(409, "The promotion was just fully redeemed; retry checkout.", "Promotion unavailable");
        }

        db.Orders.Add(order);
        db.OrderItems.AddRange(cart.Select(item => new OrderItem
        {
            OrderId = orderId,
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            UnitPrice = products[item.ProductId].Price
        }));
        db.Payments.Add(new Payment
        {
            PaymentId = Guid.NewGuid().ToString("N"),
            OrderId = orderId,
            PaymentMethod = order.PaymentMethod,
            PaymentStatus = "PENDING",
            Amount = grandTotal,
            CreatedAt = DateTime.UtcNow
        });
        db.CartItems.RemoveRange(cart);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new OrderSummary(orderId, subtotal, discount, flatFee, grandTotal, order.PaymentMethod, "PENDING", order.OrderStatus, order.CreatedAt, quote.Items);
    }

    private async Task<PreparedCheckout> PrepareAsync(string userId, string? promotionCode, CancellationToken ct)
    {
        var flatFee = configuration.GetValue<decimal>("Shipping:FlatFee");
        if (flatFee < 0) throw new InvalidOperationException("Shipping:FlatFee cannot be negative.");

        var cart = await db.CartItems.Where(x => x.UserId == userId).OrderBy(x => x.ProductId).ToListAsync(ct);
        if (cart.Count == 0) throw new ApiException(409, "The cart is empty.", "Empty cart");

        var productIds = cart.Select(x => x.ProductId).ToArray();
        var products = await db.Products.Where(x => productIds.Contains(x.ProductId) && x.IsActive).ToDictionaryAsync(x => x.ProductId, ct);
        if (products.Count != cart.Count) throw new ApiException(409, "One or more cart products are no longer available.", "Product unavailable");

        foreach (var item in cart)
        {
            if (item.Quantity <= 0 || products[item.ProductId].StockQuantity < item.Quantity)
                throw new ApiException(409, $"Insufficient stock for product {item.ProductId}.", "Insufficient stock");
        }

        var subtotal = cart.Sum(item => products[item.ProductId].Price * item.Quantity);
        const decimal maxSqlMoney = 9999999999999999.99m;
        if (subtotal > maxSqlMoney) throw new ApiException(409, "The order total exceeds the supported payment amount.", "Order total too large");
        Promotion? promotion = null;
        var discount = 0m;
        if (!string.IsNullOrWhiteSpace(promotionCode))
        {
            var code = promotionCode.Trim().ToUpperInvariant();
            promotion = await db.Promotions.SingleOrDefaultAsync(x => x.Code == code && x.IsActive, ct);
            var now = DateTime.UtcNow;
            if (promotion is null || promotion.StartAt > now || promotion.EndAt <= now || promotion.MinOrderAmount > subtotal || (promotion.UsageLimit.HasValue && promotion.UsedCount >= promotion.UsageLimit.Value))
                throw new ApiException(409, "The promotion code is invalid, expired, or not applicable.", "Promotion unavailable");

            discount = promotion.DiscountType == "PERCENT"
                ? Math.Round(subtotal * promotion.DiscountValue / 100m, 2, MidpointRounding.AwayFromZero)
                : promotion.DiscountValue;
            if (promotion.MaxDiscountAmount.HasValue) discount = Math.Min(discount, promotion.MaxDiscountAmount.Value);
            discount = Math.Min(subtotal, discount);
        }

        var grandTotal = subtotal - discount + flatFee;
        if (grandTotal > maxSqlMoney) throw new ApiException(409, "The order total exceeds the supported payment amount.", "Order total too large");
        var lines = cart.Select(item => new OrderLineResponse(item.ProductId, products[item.ProductId].ProductName, item.Quantity, products[item.ProductId].Price, products[item.ProductId].Price * item.Quantity)).ToArray();
        var fingerprintSource = string.Join("|", lines.Select(x => $"{x.ProductId}:{x.Quantity}:{x.UnitPrice.ToString("F2", CultureInfo.InvariantCulture)}:{x.ProductName.Length}:{x.ProductName}"))
            + $"|{subtotal.ToString("F2", CultureInfo.InvariantCulture)}|{discount.ToString("F2", CultureInfo.InvariantCulture)}|{flatFee.ToString("F2", CultureInfo.InvariantCulture)}|{promotion?.Code ?? ""}";
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintSource)));
        var quote = new CheckoutQuote(subtotal, discount, flatFee, grandTotal, promotion?.Code, fingerprint, lines);
        return new PreparedCheckout(cart, products, promotion, quote);
    }

    private sealed record PreparedCheckout(List<CartItem> Cart, Dictionary<int, Product> Products, Promotion? Promotion, CheckoutQuote Quote);
}
