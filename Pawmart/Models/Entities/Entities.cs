namespace Pawmart.Models.Entities;

public sealed class User
{
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = "CUSTOMER";
    public string? FcmToken { get; set; }
    public DateTime? FcmTokenUpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public sealed class Pet
{
    public int PetId { get; set; }
    public string UserId { get; set; } = "";
    public string PetName { get; set; } = "";
    public string PetType { get; set; } = "DOG";
    public string? Breed { get; set; }
    public int? AgeMonths { get; set; }
    public decimal? WeightKg { get; set; }
    public string? SpecialNotes { get; set; }
    public string? BodySize { get; set; }
    public string? ActivityLevel { get; set; }
    public int? BodyConditionScore { get; set; }
    public string? FeedingHabits { get; set; }
    public string? FoodPreferences { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class Category
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class Product
{
    public int ProductId { get; set; }
    public int CategoryId { get; set; }
    public string? Sku { get; set; }
    public string ProductName { get; set; } = "";
    public string? Brand { get; set; }
    public string? Description { get; set; }
    public string? Ingredients { get; set; }
    public string? NutritionInfo { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public int LowStockThreshold { get; set; } = 5;
    public string TargetPetType { get; set; } = "ALL";
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class ProductImage
{
    public int ImageId { get; set; }
    public int ProductId { get; set; }
    public string ImageUrl { get; set; } = "";
    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class CartItem
{
    public int CartItemId { get; set; }
    public string UserId { get; set; } = "";
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class Favorite
{
    public int FavoriteId { get; set; }
    public string UserId { get; set; } = "";
    public int ProductId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class Store
{
    public int StoreId { get; set; }
    public string StoreName { get; set; } = "";
    public string StoreType { get; set; } = "STORE";
    public string Address { get; set; } = "";
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class Promotion
{
    public int PromotionId { get; set; }
    public string Code { get; set; } = "";
    public string PromotionName { get; set; } = "";
    public string? Description { get; set; }
    public string DiscountType { get; set; } = "PERCENT";
    public decimal DiscountValue { get; set; }
    public decimal MinOrderAmount { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class Order
{
    public string OrderId { get; set; } = "";
    public string UserId { get; set; } = "";
    public int? StoreId { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal GrandTotal { get; set; }
    public int? PromotionId { get; set; }
    public string? PromotionCodeSnapshot { get; set; }
    public string ShippingAddress { get; set; } = "";
    public string RecipientName { get; set; } = "";
    public string RecipientPhone { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? DistanceKm { get; set; }
    public string PaymentMethod { get; set; } = "COD";
    public string OrderStatus { get; set; } = "PENDING";
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}

public sealed class OrderItem
{
    public int OrderItemId { get; set; }
    public string OrderId { get; set; } = "";
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; private set; }
}

public sealed class OrderStatusHistory
{
    public int HistoryId { get; set; }
    public string OrderId { get; set; } = "";
    public string Status { get; set; } = "PENDING";
    public string? Note { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class Payment
{
    public string PaymentId { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string PaymentMethod { get; set; } = "COD";
    public string PaymentStatus { get; set; } = "PENDING";
    public decimal Amount { get; set; }
    public string? TransactionCode { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? GatewayResponse { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class Notification
{
    public int NotificationId { get; set; }
    public string UserId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string? NotificationType { get; set; }
    public string? ReferenceId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
