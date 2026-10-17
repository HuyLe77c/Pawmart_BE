using Microsoft.EntityFrameworkCore;
using Pawmart.Data;
using Pawmart.Middleware;
using Pawmart.Models.Contracts.Catalog;
using Pawmart.Models.Entities;
using Pawmart.Services.Integrations;

namespace Pawmart.Services;

public sealed class RecommendationService(PawmartDbContext db, IAiRecommendationService ai)
{
    public async Task<RecommendationResult> RecommendAsync(string userId, int petId, CancellationToken ct)
    {
        var pet = await db.Pets.AsNoTracking().SingleOrDefaultAsync(x => x.PetId == petId && x.UserId == userId, ct)
            ?? throw new ApiException(404, "Pet profile was not found.", "Not found");
        var candidates = await db.Products.AsNoTracking().Where(x => x.IsActive && x.StockQuantity > 0 && (x.TargetPetType == "ALL" || x.TargetPetType == pet.PetType))
            .OrderByDescending(x => x.IsFeatured).ThenBy(x => x.ProductName).Take(40)
            .Select(x => new AiProductCandidate(x.ProductId, x.ProductName, x.TargetPetType, x.Description, x.Ingredients, x.Price)).ToListAsync(ct);
        var response = await ai.RecommendAsync(new AiRecommendationRequest(pet.PetType, pet.Breed, pet.AgeMonths, pet.WeightKg, pet.SpecialNotes, candidates), ct);
        var allowedIds = candidates.Select(x => x.ProductId).ToHashSet();
        var ids = response.RecommendedProductIds.Where(allowedIds.Contains).Distinct().Take(10).ToArray();
        var products = await db.Products.AsNoTracking().Where(x => ids.Contains(x.ProductId) && x.IsActive && x.StockQuantity > 0)
            .Select(x => new RecommendedProduct(x.ProductId, x.ProductName, x.Price, x.TargetPetType)).ToListAsync(ct);
        return new RecommendationResult(response.Advice, products);
    }
}

public sealed record RecommendedProduct(int ProductId, string ProductName, decimal Price, string TargetPetType);
public sealed record RecommendationResult(string Advice, IReadOnlyList<RecommendedProduct> Products);
