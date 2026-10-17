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
        if (!includeInactive) products = products.Where(x => x.IsActive);
        if (includeInactive && query.Active.HasValue) products = products.Where(x => x.IsActive == query.Active.Value);
        if (query.CategoryId.HasValue) products = products.Where(x => x.CategoryId == query.CategoryId);
        if (!string.IsNullOrWhiteSpace(query.PetType)) products = products.Where(x => x.TargetPetType == "ALL" || x.TargetPetType == query.PetType);
        if (!string.IsNullOrWhiteSpace(query.Brand)) products = products.Where(x => x.Brand != null && x.Brand.ToLower() == query.Brand.Trim().ToLower());
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            products = products.Where(x => x.ProductName.Contains(term) || (x.Sku != null && x.Sku.Contains(term)) || (x.Brand != null && x.Brand.Contains(term)) || (x.Description != null && x.Description.Contains(term)));
        }

        products = query.Sort switch
        {
            "price_asc" => products.OrderBy(x => x.Price).ThenBy(x => x.ProductId),
            "price_desc" => products.OrderByDescending(x => x.Price).ThenBy(x => x.ProductId),
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
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.ProductId == id && x.IsActive, ct)
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
        await EnsureCategoryExists(request.CategoryId, ct);
        var product = await db.Products.SingleOrDefaultAsync(x => x.ProductId == id, ct)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Product was not found.", "Not found");
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
        return products.Select(p => new ProductResponse(
            p.ProductId, p.CategoryId, p.Sku, p.ProductName, p.Brand, p.Description, p.Ingredients, p.NutritionInfo,
            p.Price, p.StockQuantity, p.LowStockThreshold, p.TargetPetType, p.IsFeatured, p.IsActive,
            images.Where(i => i.ProductId == p.ProductId).Select(i => new ProductImageResponse(i.ImageId, i.ImageUrl, i.IsPrimary, i.DisplayOrder)).ToArray())).ToArray();
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
        await db.Categories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.CategoryName)
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
            .Select(x => new PetResponse(x.PetId, x.PetName, x.PetType, x.Breed, x.AgeMonths, x.WeightKg, x.SpecialNotes, x.ImageUrl)).ToListAsync(ct);

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
        pet.PetName = request.PetName.Trim(); pet.PetType = request.PetType.ToUpperInvariant(); pet.Breed = request.Breed;
        pet.AgeMonths = request.AgeMonths; pet.WeightKg = request.WeightKg; pet.SpecialNotes = request.SpecialNotes; pet.ImageUrl = request.ImageUrl;
    }
    private static PetResponse ToResponse(Pet p) => new(p.PetId, p.PetName, p.PetType, p.Breed, p.AgeMonths, p.WeightKg, p.SpecialNotes, p.ImageUrl);
}
