using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Pawmart.Models.Contracts.Catalog;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ProductImageUrlAttribute : ValidationAttribute
{
    private static readonly Regex ManagedPath = new(
        @"^/uploads/products/[0-9a-f]{32}\.(jpg|png|webp)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public override bool IsValid(object? value)
    {
        if (value is not string url || string.IsNullOrWhiteSpace(url)) return false;
        if (ManagedPath.IsMatch(url)) return true;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public override string FormatErrorMessage(string name) =>
        $"{name} must be an HTTP(S) URL or a PawMart uploaded image path.";
}
