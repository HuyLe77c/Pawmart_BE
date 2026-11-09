using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Pawmart.Data;
using Pawmart.Middleware;
using Pawmart.Models.Contracts.Catalog;

namespace Pawmart.Services;

public sealed class PetImageStorageService(PawmartDbContext db, IWebHostEnvironment environment)
{
    public const long MaxImageBytes = 20L * 1024 * 1024;
    private const string PublicPrefix = "/uploads/pets/";
    private static readonly Regex ManagedFileName = new(
        @"^[0-9a-f]{32}\.(jpg|png|webp)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private string UploadDirectory => Path.Combine(environment.ContentRootPath, "wwwroot", "uploads", "pets");

    public async Task<PetResponse> UploadAsync(string userId, int petId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length <= 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "Invalid image", "Choose a non-empty image file.");
        if (file.Length > MaxImageBytes)
            throw new ApiException(StatusCodes.Status413PayloadTooLarge, "Image is too large", "The maximum image size is 20 MB.");

        var pet = await db.Pets.SingleOrDefaultAsync(x => x.PetId == petId && x.UserId == userId, ct)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "Not found", "Pet profile was not found.");
        var extension = await DetectImageExtensionAsync(file, ct);
        var imageUrl = $"{PublicPrefix}{Guid.NewGuid():N}.{extension}";
        var fullPath = Path.Combine(UploadDirectory, Path.GetFileName(imageUrl));
        var previousImageUrl = pet.ImageUrl;

        try
        {
            Directory.CreateDirectory(UploadDirectory);
            await using (var destination = new FileStream(
                fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await file.CopyToAsync(destination, ct);
            }

            pet.ImageUrl = imageUrl;
            pet.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            DeleteManagedFile(imageUrl);
            throw;
        }

        if (!string.IsNullOrWhiteSpace(previousImageUrl)) DeleteManagedFile(previousImageUrl);
        return new PetResponse(pet.PetId, pet.PetName, pet.PetType, pet.Gender, pet.Breed, pet.AgeMonths,
            pet.WeightKg, pet.SpecialNotes, pet.ImageUrl, pet.BodySize, pet.ActivityLevel,
            pet.BodyConditionScore, pet.FeedingHabits, pet.FoodPreferences);
    }

    private void DeleteManagedFile(string imageUrl)
    {
        if (!imageUrl.StartsWith(PublicPrefix, StringComparison.OrdinalIgnoreCase)) return;
        var fileName = imageUrl[PublicPrefix.Length..];
        if (!ManagedFileName.IsMatch(fileName) || fileName != Path.GetFileName(fileName)) return;
        var path = Path.Combine(UploadDirectory, fileName);
        if (File.Exists(path)) File.Delete(path);
    }

    private static async Task<string> DetectImageExtensionAsync(IFormFile file, CancellationToken ct)
    {
        var header = new byte[12];
        var read = 0;
        await using (var stream = file.OpenReadStream())
        {
            while (read < header.Length)
            {
                var count = await stream.ReadAsync(header.AsMemory(read), ct);
                if (count == 0) break;
                read += count;
            }
        }

        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return "jpg";
        if (read >= 8 && header.AsSpan(0, 8).SequenceEqual(PngSignature)) return "png";
        if (read >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            && header.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return "webp";
        throw new ApiException(StatusCodes.Status400BadRequest, "Unsupported image", "Use a valid JPEG, PNG, or WebP image.");
    }
}
