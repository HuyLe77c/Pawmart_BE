using System.ComponentModel.DataAnnotations;

namespace Pawmart.Models.Contracts.Commerce;

public sealed class PromotionWriteRequest
{
    [Required, StringLength(50, MinimumLength = 3)] public string Code { get; init; } = "";
    [Required, StringLength(150, MinimumLength = 2)] public string PromotionName { get; init; } = "";
    [StringLength(500)] public string? Description { get; init; }
    [Required, RegularExpression("^(PERCENT|FIXED)$")] public string DiscountType { get; init; } = "PERCENT";
    [Range(typeof(decimal), "0.01", "9999999999999999")] public decimal DiscountValue { get; init; }
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal MinOrderAmount { get; init; }
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal? MaxDiscountAmount { get; init; }
    public DateTimeOffset StartAt { get; init; }
    public DateTimeOffset EndAt { get; init; }
    [Range(0, int.MaxValue)] public int? UsageLimit { get; init; }
}

public sealed record PromotionResponse(int PromotionId, string Code, string PromotionName, string? Description, string DiscountType, decimal DiscountValue, decimal MinOrderAmount, decimal? MaxDiscountAmount, DateTime StartAt, DateTime EndAt, bool IsActive);

public sealed class StoreWriteRequest
{
    [Required, StringLength(150, MinimumLength = 2)] public string StoreName { get; init; } = "";
    [Required, RegularExpression("^(STORE|WAREHOUSE)$")] public string StoreType { get; init; } = "STORE";
    [Required, StringLength(500, MinimumLength = 5)] public string Address { get; init; } = "";
    [Range(typeof(decimal), "-90", "90")] public decimal Latitude { get; init; }
    [Range(typeof(decimal), "-180", "180")] public decimal Longitude { get; init; }
    [StringLength(20)] public string? PhoneNumber { get; init; }
}

public sealed record StoreResponse(int StoreId, string StoreName, string StoreType, string Address, decimal Latitude, decimal Longitude, string? PhoneNumber, bool IsActive);

public sealed class NotificationCreateRequest
{
    [Required, StringLength(150)] public string Title { get; init; } = "";
    [Required, StringLength(1000)] public string Message { get; init; } = "";
    [StringLength(30)] public string? NotificationType { get; init; }
    [StringLength(50)] public string? ReferenceId { get; init; }
}

public sealed class FcmTokenRequest
{
    [Required, StringLength(1000, MinimumLength = 10)] public string Token { get; init; } = "";
}

public sealed class SetActiveRequest { public bool IsActive { get; init; } }

public sealed record NotificationResponse(int NotificationId, string Title, string Message, string? NotificationType, string? ReferenceId, bool IsRead, DateTime CreatedAt);

public sealed class RevenueReportQuery
{
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}
public sealed record RevenueReport(DateTime PeriodStartUtc, DateTime PeriodEndUtc, decimal PeriodRevenue, decimal TodayRevenue, int OrdersToday);
public sealed record OrderStatusCount(string Status, int Count);
public sealed record LowStockProduct(int ProductId, string ProductName, int StockQuantity, int LowStockThreshold, string StockStatus);
public sealed record NotificationBroadcastResponse(int Recipients, bool Persisted, bool PushAvailable, int PushSent, string? PushDetail);
public sealed class ShippingQuoteRequest
{
    [Range(typeof(decimal), "-90", "90")] public decimal Latitude { get; init; }
    [Range(typeof(decimal), "-180", "180")] public decimal Longitude { get; init; }
}
public sealed class RecommendationRequest
{
    [Range(1, int.MaxValue)] public int PetId { get; init; }
}
