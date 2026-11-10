using Microsoft.EntityFrameworkCore;
using Pawmart.Data;
using Pawmart.Middleware;
using Pawmart.Models.Contracts.Catalog;
using Pawmart.Models.Entities;

namespace Pawmart.Services;

public sealed class ProductService(PawmartDbContext db, ProductImageStorageService imageStorage)
{
    public async Task<PagedResponse<ProductResponse>> SearchAsync(ProductQuery query, CancellationToken ct)
        => await SearchCoreAsync(query, includeInactive: false, ct);

    public Task<PagedResponse<ProductResponse>> ListForAdminAsync(ProductQuery query, CancellationToken ct)
        => SearchCoreAsync(query, includeInactive: true, ct);

    public async Task<CatalogReviewResponse> GetCatalogReviewAsync(int productId, CancellationToken ct)
    {
        var review = await db.ProductCatalogReviews.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ProductId == productId, ct)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Catalog review was not found.", "Not found");
        return new CatalogReviewResponse(review.ProductId, review.Slug, review.SubcategoryId,
            review.ModelName, review.OfficialUrl, review.ModelStatus, review.ImageStatus,
            review.PriceStatus, review.ProductStatus, review.ImageSourceUrl, review.PriceSourceUrl,
            review.PriceCheckedAt, review.ShortDescription, review.Benefits,
            review.UsageInstructions, review.Warnings, review.StorageInstructions,
            review.ReviewNote, review.PublicPreview);
    }

    public async Task<AdminProductSummary> GetAdminSummaryAsync(CancellationToken ct)
    {
        var categories = await db.Products.AsNoTracking()
            .GroupBy(x => x.CategoryId)
            .Select(group => new ProductCategorySummary(
                group.Key,
                group.Count(),
                group.Count(x => x.IsActive),
                group.Count(x => x.IsActive && x.StockQuantity <= x.LowStockThreshold)))
            .ToListAsync(ct);

        return new AdminProductSummary(
            categories.Sum(x => x.Total),
            categories.Sum(x => x.Active),
            categories.Sum(x => x.Total - x.Active),
            categories.Sum(x => x.LowStock),
            categories);
    }

    private async Task<PagedResponse<ProductResponse>> SearchCoreAsync(ProductQuery query, bool includeInactive, CancellationToken ct)
    {
        var products = db.Products.AsNoTracking();
        if (!includeInactive) products = products.Where(x => x.IsListed &&
            (x.IsActive || db.ProductCatalogReviews.Any(review =>
                review.ProductId == x.ProductId && review.PublicPreview)));
        if (includeInactive && query.Active.HasValue) products = products.Where(x => x.IsActive == query.Active.Value);
        if (query.MinPrice.HasValue || query.MaxPrice.HasValue)
            products = products.Where(x => !db.ProductCatalogReviews.Any(review => review.ProductId == x.ProductId
                && review.PriceStatus != "VERIFIED" && review.PriceStatus != "OWNER_SET"));
        products = ProductCatalogFilters.Apply(products, query);

        products = query.Sort switch
        {
            "recommended" => products.OrderBy(x => x.Sku != null && x.Sku.StartsWith("CAT41-")
                    ? x.Sku.Substring(7) : "99")
                .ThenBy(x => x.Sku),
            "price_asc" => products.OrderBy(x => x.Price <= 0).ThenBy(x => x.Price).ThenBy(x => x.ProductId),
            "price_desc" => products.OrderBy(x => x.Price <= 0).ThenByDescending(x => x.Price).ThenBy(x => x.ProductId),
            "name" => products.OrderBy(x => x.ProductName),
            _ => products.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.ProductId)
        };

        var total = await products.CountAsync(ct);
        var page = Math.Clamp(query.Page, 1, 1000);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var items = await products.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResponse<ProductResponse>(await MapProducts(items, ct), page, pageSize, total);
    }

    public async Task<ProductResponse> GetByIdAsync(int id, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.ProductId == id &&
            (x.IsActive || (x.IsListed && db.ProductCatalogReviews.Any(review =>
                review.ProductId == x.ProductId && review.PublicPreview))), ct)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Product was not found.", "Not found");
        return (await MapProducts([product], ct)).Single();
    }

    public async Task<ProductResponse> CreateAsync(ProductWriteRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await EnsureCategoryExists(request.CategoryId, ct);
        var product = new Product { CreatedAt = DateTime.UtcNow };
        Apply(product, request);
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        await ReplaceImages(product.ProductId, request.Images, ct);
        await transaction.CommitAsync(ct);
        return await GetByIdIncludingInactive(product.ProductId, ct);
    }

    public async Task<ProductResponse> UpdateAsync(int id, ProductWriteRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var product = await db.Products.SingleOrDefaultAsync(x => x.ProductId == id, ct)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Product was not found.", "Not found");
        if (request.CategoryId != product.CategoryId)
            await EnsureCategoryExists(request.CategoryId, ct);
        var previousImageUrls = await db.ProductImages.Where(x => x.ProductId == id).Select(x => x.ImageUrl).ToListAsync(ct);
        Apply(product, request);
        product.UpdatedAt = DateTime.UtcNow;
        await ReplaceImages(id, request.Images, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        var retainedImageUrls = request.Images.Select(x => x.ImageUrl.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var previousUrl in previousImageUrls.Where(x => !retainedImageUrls.Contains(x)))
            imageStorage.DeleteManagedFile(previousUrl);
        return await GetByIdIncludingInactive(id, ct);
    }

    public async Task SetActiveAsync(int id, bool active, CancellationToken ct)
    {
        var product = await db.Products.SingleOrDefaultAsync(x => x.ProductId == id, ct)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Product was not found.", "Not found");
        if (active && product.Sku?.StartsWith("CAT41-", StringComparison.Ordinal) == true)
        {
            var review = await db.ProductCatalogReviews.AsNoTracking()
                .SingleOrDefaultAsync(x => x.ProductId == id, ct);
            var hasImage = await db.ProductImages.AsNoTracking().AnyAsync(x => x.ProductId == id, ct);
            var externallyVerified = review is not null && review.ModelStatus == "VERIFIED"
                && review.PriceStatus == "VERIFIED" && review.ProductStatus == "VERIFIED"
                && !string.IsNullOrWhiteSpace(review.PriceSourceUrl) && review.PriceCheckedAt is not null;
            var ownerApproved = review is not null && review.PriceStatus == "OWNER_SET"
                && review.ProductStatus == "OWNER_APPROVED";
            if (review is null || review.ImageStatus != "VERIFIED"
                || string.IsNullOrWhiteSpace(review.ImageSourceUrl) || !hasImage
                || !(externallyVerified || ownerApproved)
                || product.Price <= 0 || product.StockQuantity <= 0)
                throw new ApiException(StatusCodes.Status400BadRequest,
                    "Product requires a reviewed image, approved sale price, and positive stock before activation.", "Catalog review required");
        }
        product.IsActive = active;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task<ProductResponse> GetByIdIncludingInactive(int id, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking().SingleAsync(x => x.ProductId == id, ct);
        return (await MapProducts([product], ct)).Single();
    }

    private async Task<IReadOnlyList<ProductResponse>> MapProducts(IReadOnlyList<Product> products, CancellationToken ct)
    {
        var ids = products.Select(x => x.ProductId).ToArray();
        var images = await db.ProductImages.AsNoTracking().Where(x => ids.Contains(x.ProductId))
            .OrderByDescending(x => x.IsPrimary).ThenBy(x => x.DisplayOrder)
            .ToListAsync(ct);
        var reviewedIds = products.Where(x => x.Sku?.StartsWith("CAT41-", StringComparison.Ordinal) == true)
            .Select(x => x.ProductId).ToArray();
        List<ProductCatalogReview> reviews = reviewedIds.Length == 0
            ? []
            : await db.ProductCatalogReviews.AsNoTracking()
                .Where(x => reviewedIds.Contains(x.ProductId)).ToListAsync(ct);
        var reviewByProduct = reviews.ToDictionary(x => x.ProductId);
        return products.Select(p => new ProductResponse(
            p.ProductId, p.CategoryId, p.Sku, p.ProductName, p.Brand, p.Description, p.Ingredients, p.NutritionInfo,
            p.Price, p.StockQuantity, p.LowStockThreshold, p.TargetPetType, p.IsFeatured, p.IsActive,
            images.Where(i => i.ProductId == p.ProductId).Select(i => new ProductImageResponse(i.ImageId, i.ImageUrl, i.IsPrimary, i.DisplayOrder)).ToArray(),
            p.PackageSize, p.LifeStage, p.FeedingGuide,
            reviewByProduct.TryGetValue(p.ProductId, out var review)
                ? new CatalogReviewSummary(review.ModelStatus, review.ImageStatus,
                    review.PriceStatus, review.ProductStatus, review.PublicPreview,
                    review.OfficialUrl, review.ShortDescription)
                : null)).ToArray();
    }

    private async Task ReplaceImages(int productId, IReadOnlyCollection<ProductImageWriteRequest> requested, CancellationToken ct)
    {
        if (requested.Count > 20) throw new ApiException(400, "A product can have at most 20 images.", "Validation failed");
        if (requested.Count(x => x.IsPrimary) > 1) throw new ApiException(400, "Only one image can be primary.", "Validation failed");
        var existing = await db.ProductImages.Where(x => x.ProductId == productId).ToListAsync(ct);
        db.ProductImages.RemoveRange(existing);
        if (existing.Count > 0) await db.SaveChangesAsync(ct);
        var orderValues = requested.Select(x => x.DisplayOrder).ToArray();
        if (orderValues.Distinct().Count() != orderValues.Length) throw new ApiException(400, "Image display orders must be unique.", "Validation failed");
        foreach (var image in requested)
            db.ProductImages.Add(new ProductImage { ProductId = productId, ImageUrl = image.ImageUrl.Trim(), IsPrimary = image.IsPrimary, DisplayOrder = image.DisplayOrder, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureCategoryExists(int id, CancellationToken ct)
    {
        if (!await db.Categories.AnyAsync(x => x.CategoryId == id && x.IsActive, ct))
            throw new ApiException(400, "The selected category is unavailable.", "Invalid category");
    }

    private static void Apply(Product product, ProductWriteRequest request)
    {
        product.CategoryId = request.CategoryId;
        product.Sku = string.IsNullOrWhiteSpace(request.Sku) ? null : request.Sku.Trim();
        product.ProductName = request.ProductName.Trim();
        product.Brand = string.IsNullOrWhiteSpace(request.Brand) ? null : request.Brand.Trim();
        product.Description = request.Description;
        product.Ingredients = request.Ingredients;
        product.NutritionInfo = request.NutritionInfo;
        product.PackageSize = string.IsNullOrWhiteSpace(request.PackageSize) ? null : request.PackageSize.Trim();
        product.LifeStage = string.IsNullOrWhiteSpace(request.LifeStage) ? null : request.LifeStage.Trim();
        product.FeedingGuide = string.IsNullOrWhiteSpace(request.FeedingGuide) ? null : request.FeedingGuide.Trim();
        product.Price = request.Price;
        product.StockQuantity = request.StockQuantity;
        product.LowStockThreshold = request.LowStockThreshold;
        product.TargetPetType = request.TargetPetType.ToUpperInvariant();
        product.IsFeatured = request.IsFeatured;
        product.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed class CategoryService(PawmartDbContext db)
{
    public async Task<IReadOnlyList<CategoryResponse>> ListAsync(CancellationToken ct) =>
        await db.Categories.AsNoTracking()
            .Where(x => x.IsActive && db.Products.Any(product => product.CategoryId == x.CategoryId &&
                product.IsListed && (product.IsActive || db.ProductCatalogReviews.Any(review =>
                    review.ProductId == product.ProductId && review.PublicPreview))))
            .OrderBy(x => x.SortOrder).ThenBy(x => x.CategoryName)
            .Select(x => new CategoryResponse(x.CategoryId, x.CategoryName, x.ImageUrl, x.Description, x.SortOrder, x.IsActive)).ToListAsync(ct);

    public async Task<IReadOnlyList<CategoryResponse>> ListForAdminAsync(CancellationToken ct) =>
        await db.Categories.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.CategoryId)
            .Select(x => new CategoryResponse(x.CategoryId, x.CategoryName, x.ImageUrl, x.Description, x.SortOrder, x.IsActive)).ToListAsync(ct);

    public async Task<CategoryResponse> CreateAsync(CategoryWriteRequest request, CancellationToken ct)
    {
        var category = new Category { CategoryName = request.CategoryName.Trim(), ImageUrl = request.ImageUrl, Description = request.Description, SortOrder = request.SortOrder, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return ToResponse(category);
    }

    public async Task<CategoryResponse> UpdateAsync(int id, CategoryWriteRequest request, CancellationToken ct)
    {
        var category = await db.Categories.SingleOrDefaultAsync(x => x.CategoryId == id, ct)
            ?? throw new ApiException(404, "Category was not found.", "Not found");
        category.CategoryName = request.CategoryName.Trim(); category.ImageUrl = request.ImageUrl; category.Description = request.Description; category.SortOrder = request.SortOrder; category.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToResponse(category);
    }

    public async Task SetActiveAsync(int id, bool active, CancellationToken ct)
    {
        var category = await db.Categories.SingleOrDefaultAsync(x => x.CategoryId == id, ct)
            ?? throw new ApiException(404, "Category was not found.", "Not found");
        category.IsActive = active; category.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static CategoryResponse ToResponse(Category x) => new(x.CategoryId, x.CategoryName, x.ImageUrl, x.Description, x.SortOrder, x.IsActive);
}

public sealed class PetService(PawmartDbContext db)
{
    public async Task<IReadOnlyList<PetResponse>> ListAsync(string userId, CancellationToken ct) =>
        await db.Pets.AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.PetId)
            .Select(x => new PetResponse(x.PetId, x.PetName, x.PetType, x.Gender ?? "UNKNOWN", x.Breed, x.AgeMonths, x.WeightKg,
                x.SpecialNotes, x.ImageUrl, x.BodySize, x.ActivityLevel, x.BodyConditionScore, x.FeedingHabits, x.FoodPreferences)).ToListAsync(ct);

    public async Task<PetResponse> CreateAsync(string userId, PetWriteRequest request, CancellationToken ct)
    {
        var pet = new Pet { UserId = userId, CreatedAt = DateTime.UtcNow };
        Apply(pet, request);
        db.Pets.Add(pet);
        await db.SaveChangesAsync(ct);
        return ToResponse(pet);
    }

    public async Task<PetResponse> UpdateAsync(string userId, int petId, PetWriteRequest request, CancellationToken ct)
    {
        var pet = await db.Pets.SingleOrDefaultAsync(x => x.UserId == userId && x.PetId == petId, ct)
            ?? throw new ApiException(404, "Pet profile was not found.", "Not found");
        Apply(pet, request); pet.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToResponse(pet);
    }

    public async Task DeleteAsync(string userId, int petId, CancellationToken ct)
    {
        var pet = await db.Pets.SingleOrDefaultAsync(x => x.UserId == userId && x.PetId == petId, ct)
            ?? throw new ApiException(404, "Pet profile was not found.", "Not found");
        db.Pets.Remove(pet);
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(Pet pet, PetWriteRequest request)
    {
        pet.PetName = request.PetName.Trim(); pet.PetType = request.PetType.ToUpperInvariant(); pet.Gender = request.Gender.ToUpperInvariant(); pet.Breed = request.Breed;
        pet.AgeMonths = request.AgeMonths; pet.WeightKg = request.WeightKg; pet.SpecialNotes = request.SpecialNotes;
        if (request.ImageUrl is not null) pet.ImageUrl = request.ImageUrl;
        pet.BodySize = request.BodySize; pet.ActivityLevel = request.ActivityLevel;
        pet.BodyConditionScore = request.BodyConditionScore;
        pet.FeedingHabits = request.FeedingHabits?.Trim(); pet.FoodPreferences = request.FoodPreferences?.Trim();
    }
    private static PetResponse ToResponse(Pet p) => new(p.PetId, p.PetName, p.PetType, p.Gender ?? "UNKNOWN", p.Breed, p.AgeMonths, p.WeightKg,
        p.SpecialNotes, p.ImageUrl, p.BodySize, p.ActivityLevel, p.BodyConditionScore, p.FeedingHabits, p.FoodPreferences);
}
