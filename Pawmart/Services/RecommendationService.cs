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
        var notes = pet.SpecialNotes ?? "";
        if (PetHealthSafety.IsUrgent(notes))
            return new RecommendationResult("Triệu chứng trong hồ sơ có thể cần cấp cứu. Hãy liên hệ hoặc đưa bé đến bác sĩ thú y ngay; không tự chọn thức ăn hay thuốc để xử lý tình trạng này.", []) { Sources = PetNutritionEvidence.Emergency };
        if (PetHealthSafety.NeedsPrescriptionDiet(notes))
            return new RecommendationResult("Tình trạng bệnh trong hồ sơ cần chế độ ăn theo đánh giá của bác sĩ thú y. PawMart chưa có dữ liệu dinh dưỡng điều trị đủ để xác nhận sản phẩm nào phù hợp; hãy hỏi bác sĩ đang theo dõi bé trước khi đổi thức ăn.", []) { Sources = PetNutritionEvidence.General };
        if (pet.BodyConditionScore is < 4 or > 5)
            return new RecommendationResult($"Điểm thể trạng {pet.BodyConditionScore}/9 nằm ngoài khoảng 4–5/9 thường dùng để đánh giá thể trạng phù hợp. Không thể kết luận khẩu phần hay loại thức ăn giảm/tăng cân chỉ từ cân nặng hiện tại; hãy để bác sĩ thú y đánh giá thể trạng và lập kế hoạch ăn.", []) { Sources = PetNutritionEvidence.General };

        var allergies = PetHealthSafety.Allergies(notes);
        var disliked = PetHealthSafety.AvoidedIngredients((pet.FoodPreferences ?? "") + ". " + notes);
        var allCandidates = await db.Products.AsNoTracking()
            .Join(db.Categories.AsNoTracking(), p => p.CategoryId, c => c.CategoryId, (p, c) => new { Product = p, Category = c })
            .Where(x => x.Product.IsActive && x.Product.StockQuantity > 0 &&
                (x.Product.TargetPetType == "ALL" || x.Product.TargetPetType == pet.PetType) &&
                (x.Category.CategoryName.Contains("Food") || x.Category.CategoryName.Contains("Treat")))
            .OrderBy(x => x.Category.CategoryName.Contains("Treat"))
            .ThenByDescending(x => x.Product.IsFeatured).ThenBy(x => x.Product.ProductName).Take(40)
            .Select(x => new AiProductCandidate(x.Product.ProductId, x.Product.ProductName, x.Product.TargetPetType,
                x.Product.Description, x.Product.Ingredients, x.Product.Price,
                x.Category.CategoryName.Contains("Treat"))).ToListAsync(ct);
        var allergyCandidates = allCandidates.Where(x =>
            (pet.AgeMonths is null || (pet.AgeMonths < 12
                ? !x.Name.Contains("trưởng thành", StringComparison.OrdinalIgnoreCase) && !x.Name.Contains("adult", StringComparison.OrdinalIgnoreCase)
                : !x.Name.Contains("mèo con", StringComparison.OrdinalIgnoreCase) && !x.Name.Contains("chó con", StringComparison.OrdinalIgnoreCase) && !x.Name.Contains("kitten", StringComparison.OrdinalIgnoreCase) && !x.Name.Contains("puppy", StringComparison.OrdinalIgnoreCase))) &&
            (allergies.Count == 0 || (!string.IsNullOrWhiteSpace(x.Ingredients) && !PetHealthSafety.ContainsAllergen(x.Ingredients, allergies)))).ToList();
        var candidates = allergyCandidates.Where(x => disliked.Count == 0 ||
            (!string.IsNullOrWhiteSpace(x.Ingredients) && !PetHealthSafety.ContainsAllergen(x.Ingredients, disliked))).ToList();
        var preferred = PetHealthSafety.PreferredIngredients(pet.FoodPreferences ?? "");
        var preferredCandidates = candidates.Where(x => !string.IsNullOrWhiteSpace(x.Ingredients) &&
            PetHealthSafety.ContainsAllergen(x.Ingredients, preferred)).ToList();
        if (preferredCandidates.Any(x => !x.IsTreat)) candidates = preferredCandidates;
        if (allergies.Count > 0)
        {
            if (allergyCandidates.Count == 0)
                return new RecommendationResult($"Không có sản phẩm đang bán nào có danh sách thành phần đủ rõ để loại trừ {string.Join(", ", allergies)}. Hãy hỏi bác sĩ thú y và kiểm tra nhãn cùng nguy cơ nhiễm chéo trước khi cho bé ăn.", []) { Sources = PetNutritionEvidence.General };
            if (candidates.Count == 0)
                return new RecommendationResult("Chưa có sản phẩm đáp ứng đồng thời ghi chú dị ứng và sở thích tránh thành phần trong hồ sơ. Hãy kiểm tra lại hồ sơ và hỏi bác sĩ thú y trước khi chọn thức ăn.", []) { Sources = PetNutritionEvidence.General };
            var allergyProducts = candidates.Take(10).Select(x => new RecommendedProduct(x.ProductId, x.Name, x.Price, x.TargetPetType)).ToArray();
            return new RecommendationResult($"Bé có ghi nhận dị ứng {string.Join(", ", allergies)}. Các sản phẩm dưới đây không ghi thành phần đó trong dữ liệu PawMart, nhưng điều này không chứng minh an toàn dị ứng hoặc không nhiễm chéo. Sản phẩm ghi 'thưởng' chỉ là đồ thưởng, không thay bữa ăn chính. Hãy kiểm tra nhãn thực tế và hỏi bác sĩ thú y trước khi dùng.", allergyProducts) { Sources = PetNutritionEvidence.General };
        }
        if (candidates.Count == 0)
            return new RecommendationResult(disliked.Count > 0
                ? $"Chưa có sản phẩm đang bán phù hợp với loại, độ tuổi và sở thích tránh {string.Join(", ", disliked)} của bé. Sở thích có thể cập nhật trong hồ sơ; không dùng đồ thưởng thay thức ăn chính."
                : "Chưa có sản phẩm thức ăn phù hợp với loại và độ tuổi của bé trong danh mục đang bán.", []) { Sources = PetNutritionEvidence.General };
        AiRecommendationResponse response;
        var usedFallback = false;
        try
        {
            response = await ai.RecommendAsync(new AiRecommendationRequest(pet.PetType, pet.Gender ?? "UNKNOWN", pet.Breed, pet.AgeMonths,
                pet.WeightKg, pet.SpecialNotes, pet.BodySize, pet.ActivityLevel, pet.BodyConditionScore,
                pet.FeedingHabits, pet.FoodPreferences, candidates), ct);
        }
        catch (ApiException ex) when (ex.StatusCode is 502 or 503)
        {
            response = new AiRecommendationResponse("", []);
            usedFallback = true;
        }
        var allowedIds = candidates.Select(x => x.ProductId).ToHashSet();
        var selectedIds = response.RecommendedProductIds.Where(allowedIds.Contains).Distinct().Take(3).ToArray();
        var mainFood = candidates.Where(x => !x.IsTreat).ToList();
        var ids = selectedIds.Length == 0 || (mainFood.Count > 0 && selectedIds.All(id => candidates.First(x => x.ProductId == id).IsTreat))
            ? (mainFood.Count > 0 ? mainFood : candidates).Take(2).Select(x => x.ProductId).ToArray()
            : selectedIds;
        if (selectedIds.Length == 0) usedFallback = true;
        var products = await db.Products.AsNoTracking().Where(x => ids.Contains(x.ProductId) && x.IsActive && x.StockQuantity > 0)
            .Select(x => new RecommendedProduct(x.ProductId, x.ProductName, x.Price, x.TargetPetType)).ToListAsync(ct);
        var names = products.Select(x => x.ProductName).ToArray();
        var profile = DescribeProfile(pet);
        var onlyTreats = products.Count > 0 && products.All(p => candidates.First(x => x.ProductId == p.ProductId).IsTreat);
        var advice = names.Length == 0
            ? "Chưa có sản phẩm phù hợp đủ rõ trong danh mục hiện tại. Bạn có thể bổ sung tuổi, cân nặng và ghi chú sức khỏe của bé hoặc hỏi bác sĩ thú y nếu bé đang có bệnh."
            : onlyTreats
                ? $"{profile} Chưa tìm thấy thức ăn chính phù hợp với các tiêu chí đã ghi. Chỉ có đồ thưởng để xem xét: {string.Join(", ", names)}. Đồ thưởng không thay bữa chính; hãy kiểm tra nhãn và hỏi bác sĩ thú y nếu bé có vấn đề sức khỏe."
                : $"{profile} Có thể xem xét: {string.Join(", ", names)}. Đây là gợi ý từ danh mục, chưa phải xác nhận thức ăn hoàn chỉnh hoặc khẩu phần phù hợp. Hãy kiểm tra nhãn về giai đoạn sống và thành phần; đồ thưởng không thay bữa chính. PawMart chưa có kcal/100g của sản phẩm nên không tính lượng ăn mỗi ngày.";
        if (usedFallback)
            advice = "Omniroute chưa trả gợi ý dùng được; đây là danh sách dự phòng dựa trên hồ sơ và dữ liệu sản phẩm đã kiểm tra. " + advice;
        return new RecommendationResult(advice, products) { Sources = PetNutritionEvidence.General };
    }

    private static string DescribeProfile(Pet pet)
    {
        var facts = new List<string> { pet.PetType == "CAT" ? "mèo" : pet.PetType == "DOG" ? "chó" : "thú cưng" };
        if (pet.Gender is "MALE" or "FEMALE") facts.Add(pet.Gender == "MALE" ? "đực" : "cái");
        if (!string.IsNullOrWhiteSpace(pet.Breed)) facts.Add($"giống {pet.Breed.Trim()}");
        if (pet.AgeMonths is not null) facts.Add($"{pet.AgeMonths} tháng tuổi");
        if (pet.WeightKg is not null) facts.Add($"{pet.WeightKg:0.##} kg");
        if (pet.BodySize is not null) facts.Add("size " + (pet.BodySize switch { "SMALL" => "nhỏ", "MEDIUM" => "vừa", "LARGE" => "lớn", _ => pet.BodySize }));
        if (pet.ActivityLevel is not null) facts.Add("vận động " + (pet.ActivityLevel switch { "LOW" => "ít", "MODERATE" => "vừa", "HIGH" => "nhiều", _ => pet.ActivityLevel }));
        if (pet.BodyConditionScore is not null) facts.Add($"thể trạng {pet.BodyConditionScore}/9");
        if (!string.IsNullOrWhiteSpace(pet.FeedingHabits)) facts.Add($"thói quen: {pet.FeedingHabits.Trim()}");
        if (!string.IsNullOrWhiteSpace(pet.FoodPreferences)) facts.Add($"sở thích: {pet.FoodPreferences.Trim()}");
        return "Hồ sơ: " + string.Join(", ", facts) + ".";
    }
}

public sealed record RecommendedProduct(int ProductId, string ProductName, decimal Price, string TargetPetType);
public sealed record RecommendationResult(string Advice, IReadOnlyList<RecommendedProduct> Products)
{
    public IReadOnlyList<NutritionSource> Sources { get; init; } = [];
}
