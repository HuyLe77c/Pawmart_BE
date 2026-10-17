using System.ComponentModel.DataAnnotations;

namespace Pawmart.Models.Contracts.Orders;

public sealed class OrderListQuery
{
    [Range(1, 1000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [RegularExpression("^(PENDING|CONFIRMED|SHIPPING|DELIVERED|CANCELLED)$")] public string? Status { get; init; }
}

public sealed class OrderStatusRequest
{
    [Required, RegularExpression("^(CONFIRMED|SHIPPING|DELIVERED|CANCELLED)$")] public string Status { get; init; } = "";
}

public sealed record OrderListItemResponse(string OrderId, decimal Total, string PaymentMethod, string PaymentStatus, string Status, DateTime CreatedAt);
public sealed record OrderStatusHistoryResponse(string Status, string? Note, DateTime CreatedAt);
public sealed record OrderDetailResponse(
    string OrderId, decimal Subtotal, decimal Discount, decimal ShippingFee, decimal Total,
    string ShippingAddress, string RecipientName, string RecipientPhone, decimal? Latitude, decimal? Longitude,
    string PaymentMethod, string PaymentStatus, string Status, string? Note, DateTime CreatedAt,
    IReadOnlyList<OrderLineResponse> Items, IReadOnlyList<OrderStatusHistoryResponse> History);
