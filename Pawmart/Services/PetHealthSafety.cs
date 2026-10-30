using System.Text.RegularExpressions;

namespace Pawmart.Services;

internal static class PetHealthSafety
{
    private static readonly string[] UrgentSigns = [
        "ra máu", "máu trong phân", "nôn liên tục", "nôn nhiều", "co giật", "khó thở",
        "ngộ độc", "ăn phải thuốc", "ăn phải chocolate", "bí tiểu", "không tiểu được",
        "bất tỉnh", "lờ đờ", "li bì", "bloody stool", "seizure", "poison"
    ];
    private static readonly string[] ManagedConditions = [
        "bệnh thận", "suy thận", "bệnh gan", "suy gan", "bệnh tim", "tiểu đường",
        "viêm tụy", "sỏi tiết niệu", "bệnh đường tiết niệu", "kidney disease",
        "renal disease", "diabetes", "pancreatitis", "tiêu chảy", "nôn", "bỏ ăn",
        "sụt cân", "béo phì", "thừa cân", "dị ứng da", "vomiting", "diarrhea"
    ];
    private static readonly (string Label, string[] Terms)[] Allergens = [
        ("gà", ["gà", "chicken"]), ("bò", ["bò", "beef"]),
        ("cá", ["cá", "fish", "cá hồi", "cá ngừ"]), ("sữa", ["sữa", "milk", "dairy"]),
        ("trứng", ["trứng", "egg"]), ("ngô", ["ngô", "bắp", "corn"]),
        ("lúa mì", ["lúa mì", "wheat"]), ("gạo", ["gạo", "rice"]),
        ("đậu nành", ["đậu nành", "soy"])
    ];

    public static bool IsUrgent(string text) => HasAny(text, UrgentSigns);
    public static bool NeedsPrescriptionDiet(string text) => HasAny(text, ManagedConditions);

    public static IReadOnlyList<string> Allergies(string text)
    {
        if (!Regex.IsMatch(text, @"(?i)(?<!không )(?:dị ứng|allerg(?:y|ic))")) return [];
        var spans = ExtractAfter(text, @"(?:dị ứng|allerg(?:y|ic)(?: to)?)");
        return Allergens.Where(a => spans.Any(s => a.Terms.Any(t => HasWholeTerm(s, t))))
            .Select(a => a.Label).ToArray();
    }

    public static IReadOnlyList<string> AvoidedIngredients(string text)
    {
        var spans = ExtractAfter(text, @"(?:không thích|ghét|tránh)");
        return Allergens.Where(a => spans.Any(s => a.Terms.Any(t => HasWholeTerm(s, t))))
            .Select(a => a.Label).ToArray();
    }

    public static IReadOnlyList<string> PreferredIngredients(string text)
    {
        var spans = ExtractAfter(text, @"(?<!không )thích");
        return Allergens.Where(a => spans.Any(s => a.Terms.Any(t => HasWholeTerm(s, t))))
            .Select(a => a.Label).ToArray();
    }

    public static bool ContainsAllergen(string ingredients, IReadOnlyList<string> allergies)
    {
        foreach (var allergy in allergies)
        {
            var item = Allergens.FirstOrDefault(a => a.Label == allergy);
            if (item.Terms is not null && item.Terms.Any(t => HasWholeTerm(ingredients, t))) return true;
        }
        return false;
    }

    private static bool HasAny(string text, IEnumerable<string> terms) =>
        terms.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> ExtractAfter(string text, string marker) =>
        Regex.Matches(text.ToLowerInvariant(), marker + @"\s+([^,.;?!\n]{1,60})", RegexOptions.IgnoreCase)
            .Select(m => Regex.Split(m.Groups[1].Value, @"\b(?:nên|cần|muốn|thì|để|vậy|cho nên)\b", RegexOptions.IgnoreCase)[0])
            .ToArray();

    private static bool HasWholeTerm(string text, string term) =>
        Regex.IsMatch(text, @"(?<!\p{L})" + Regex.Escape(term) + @"(?!\p{L})", RegexOptions.IgnoreCase);
}
