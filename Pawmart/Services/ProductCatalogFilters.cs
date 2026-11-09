using Pawmart.Models.Contracts.Catalog;
using Pawmart.Models.Entities;

namespace Pawmart.Services;

public static class ProductCatalogFilters
{
    // Apply before counting and paging, so every page observes the same filters.
    public static IQueryable<Product> Apply(IQueryable<Product> products, ProductQuery query)
    {
        if (query.CategoryId.HasValue) products = products.Where(x => x.CategoryId == query.CategoryId);
        if (!string.IsNullOrWhiteSpace(query.PetType) && query.PetType != "ALL")
            products = products.Where(x => x.TargetPetType == "ALL" || x.TargetPetType == query.PetType);
        if (!string.IsNullOrWhiteSpace(query.Brand))
        {
            var brand = query.Brand.Trim().ToLower();
            products = products.Where(x => x.Brand != null && x.Brand.ToLower() == brand);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            products = products.Where(x => x.ProductName.Contains(term) || (x.Sku != null && x.Sku.Contains(term))
                || (x.Brand != null && x.Brand.Contains(term)) || (x.Description != null && x.Description.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(query.Breed))
        {
            // This is a text match, not a claim of veterinary/breed suitability.
            var breed = query.Breed.Trim().ToLower();
            products = products.Where(x => x.ProductName.ToLower().Contains(breed)
                || (x.Description != null && x.Description.ToLower().Contains(breed)));
        }
        if (query.MinPrice.HasValue || query.MaxPrice.HasValue)
            products = products.Where(x => x.Price > 0);
        if (query.MinPrice.HasValue) products = products.Where(x => x.Price >= query.MinPrice.Value);
        if (query.MaxPrice.HasValue) products = products.Where(x => x.Price <= query.MaxPrice.Value);
        if (query.InStock == true) products = products.Where(x => x.IsActive && x.StockQuantity > 0);
        return products;
    }
}
