using System.ComponentModel.DataAnnotations;

namespace Pawmart.Models.Contracts.Orders;

public sealed class CheckoutRequest
{
    [Required, StringLength(500, MinimumLength = 5)] public string ShippingAddress { get; init; } = "";
    [Required, StringLength(100, MinimumLength = 2)] public string RecipientName { get; init; } = "";
    [Required, StringLength(20, MinimumLength = 7)] public string RecipientPhone { get; init; } = "";
    [Range(typeof(decimal), "-90", "90")] public decimal? Latitude { get; init; }
    [Range(typeof(decimal), "-180", "180")] public decimal? Longitude { get; init; }
    [Required, RegularExpression("^(COD|VNPAY|MOMO|ZALOPAY)$")] public string PaymentMethod { get; init; } = "COD";
    [StringLength(50)] public string? PromotionCode { get; init; }
    [StringLength(64, MinimumLength = 64)] public string? ExpectedQuoteFingerprint { get; init; }
    [StringLength(500)] public string? Note { get; init; }
}

public sealed class CheckoutQuoteRequest
{
    [StringLength(50)] public string? PromotionCode { get; init; }
}

public sealed record OrderLineResponse(int ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);
public sealed record CheckoutQuote(decimal Subtotal, decimal Discount, decimal ShippingFee, decimal Total, string? PromotionCode, string Fingerprint, IReadOnlyList<OrderLineResponse> Items);
public sealed record OrderSummary(string OrderId, decimal Subtotal, decimal Discount, decimal ShippingFee, decimal Total, string PaymentMethod, string PaymentStatus, string Status, DateTime CreatedAt, IReadOnlyList<OrderLineResponse> Items);
