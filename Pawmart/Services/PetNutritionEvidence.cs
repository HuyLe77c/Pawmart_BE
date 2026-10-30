namespace Pawmart.Services;

public sealed record NutritionSource(string Title, string Url);

internal static class PetNutritionEvidence
{
    // Curated from primary veterinary and regulator sources on 2026-10-28.
    // These are reference links, not a live web search or product certification.
    public static readonly IReadOnlyList<NutritionSource> General = [
        new("WSAVA - Global Nutrition Guidelines", "https://wsava.org/Global-Guidelines/Global-Nutrition-Guidelines/"),
        new("FDA - Complete and Balanced Pet Food", "https://www.fda.gov/animal-veterinary/animal-health-literacy/complete-and-balanced-pet-food"),
        new("Merck Veterinary Manual - Nutritional Requirements", "https://www.merckvetmanual.com/management-and-nutrition/nutrition-small-animals/nutritional-requirements-of-small-animals")
    ];

    public static readonly IReadOnlyList<NutritionSource> Kidney = [
        new("VCA - Nutrition for Dogs with Chronic Kidney Disease", "https://vcahospitals.com/know-your-pet/nutrition-for-dogs-with-chronic-kidney-disease")
    ];

    public static readonly IReadOnlyList<NutritionSource> Emergency = [
        new("VCA - Urgent Care for Diarrhea or Vomiting", "https://vcahospitals.com/urgent-care/health-concerns/diarrhea-or-vomiting")
    ];
}
