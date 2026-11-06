using System.ComponentModel.DataAnnotations;

namespace Pawmart.Models.Contracts.Catalog;

public sealed class ProductWriteRequest
{
    [Range(1, int.MaxValue)] public int CategoryId { get; init; }
    [StringLength(50)] public string? Sku { get; init; }
    [Required, StringLength(150, MinimumLength = 2)] public string ProductName { get; init; } = "";
    [StringLength(100)] public string? Brand { get; init; }
    public string? Description { get; init; }
    public string? Ingredients { get; init; }
    public string? NutritionInfo { get; init; }
    [StringLength(100)] public string? PackageSize { get; init; }
    [StringLength(100)] public string? LifeStage { get; init; }
    public string? FeedingGuide { get; init; }
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal Price { get; init; }
    [Range(0, int.MaxValue)] public int StockQuantity { get; init; }
    [Range(0, int.MaxValue)] public int LowStockThreshold { get; init; } = 5;
    [RegularExpression("^(DOG|CAT|ALL|OTHER)$")] public string TargetPetType { get; init; } = "ALL";
    public bool IsFeatured { get; init; }
    public List<ProductImageWriteRequest> Images { get; init; } = [];
}

public sealed class ProductImageWriteRequest
{
    [Required, StringLength(1000), ProductImageUrl] public string ImageUrl { get; init; } = "";
    public bool IsPrimary { get; init; }
    [Range(0, int.MaxValue)] public int DisplayOrder { get; init; }
}

public sealed record ProductResponse(
    int ProductId, int CategoryId, string? Sku, string ProductName, string? Brand, string? Description,
    string? Ingredients, string? NutritionInfo, decimal Price, int StockQuantity, int LowStockThreshold,
    string TargetPetType, bool IsFeatured, bool IsActive, IReadOnlyList<ProductImageResponse> Images,
    string? PackageSize, string? LifeStage, string? FeedingGuide,
    CatalogReviewSummary? CatalogReview = null);

public sealed record CatalogReviewSummary(string ModelStatus, string ImageStatus,
    string PriceStatus, string ProductStatus, bool PublicPreview,
    string OfficialUrl, string? ShortDescription);

public sealed record ProductImageResponse(int ImageId, string ImageUrl, bool IsPrimary, int DisplayOrder);

public sealed record CatalogReviewResponse(
    int ProductId, string Slug, int SubcategoryId, string ModelName, string OfficialUrl,
    string ModelStatus, string ImageStatus, string PriceStatus, string ProductStatus,
    string? ImageSourceUrl, string? PriceSourceUrl, DateTime? PriceCheckedAt,
    string? ShortDescription, string? Benefits, string? UsageInstructions,
    string? Warnings, string? StorageInstructions, string? ReviewNote, bool PublicPreview);

public sealed class ProductQuery
{
    [StringLength(120)] public string? Search { get; init; }
    [Range(1, int.MaxValue)] public int? CategoryId { get; init; }
    [RegularExpression("^(DOG|CAT|ALL|OTHER)$")] public string? PetType { get; init; }
    public bool? Active { get; init; }
    [StringLength(100)] public string? Brand { get; init; }
    [Range(1, 1000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [RegularExpression("^(recommended|price_asc|price_desc|name|newest)$")] public string Sort { get; init; } = "newest";
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems);

public sealed record ProductCategorySummary(int CategoryId, int Total, int Active, int LowStock);

public sealed record AdminProductSummary(
    int Total, int Active, int Inactive, int LowStock,
    IReadOnlyList<ProductCategorySummary> Categories);

public sealed class CategoryWriteRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string CategoryName { get; init; } = "";
    [StringLength(1000), Url] public string? ImageUrl { get; init; }
    [StringLength(500)] public string? Description { get; init; }
    [Range(0, int.MaxValue)] public int SortOrder { get; init; }
}

public sealed record CategoryResponse(int CategoryId, string CategoryName, string? ImageUrl, string? Description, int SortOrder, bool IsActive);

public sealed class PetWriteRequest
{
    [Required, StringLength(50, MinimumLength = 1)] public string PetName { get; init; } = "";
    [Required, RegularExpression("^(DOG|CAT|OTHER)$")] public string PetType { get; init; } = "DOG";
    [StringLength(100)] public string? Breed { get; init; }
    [Range(0, 600)] public int? AgeMonths { get; init; }
    [Range(typeof(decimal), "0.01", "999.99")] public decimal? WeightKg { get; init; }
    [StringLength(2000)] public string? SpecialNotes { get; init; }
    [RegularExpression("^(SMALL|MEDIUM|LARGE)$")] public string? BodySize { get; init; }
    [RegularExpression("^(LOW|MODERATE|HIGH)$")] public string? ActivityLevel { get; init; }
    [Range(1, 9)] public int? BodyConditionScore { get; init; }
    [StringLength(500)] public string? FeedingHabits { get; init; }
    [StringLength(500)] public string? FoodPreferences { get; init; }
    [StringLength(1000), Url] public string? ImageUrl { get; init; }
}

public sealed record PetResponse(int PetId, string PetName, string PetType, string? Breed, int? AgeMonths, decimal? WeightKg,
    string? SpecialNotes, string? ImageUrl, string? BodySize, string? ActivityLevel, int? BodyConditionScore,
    string? FeedingHabits, string? FoodPreferences);
