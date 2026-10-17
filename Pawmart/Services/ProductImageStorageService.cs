using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Pawmart.Data;
using Pawmart.Middleware;
using Pawmart.Models.Contracts.Catalog;
using Pawmart.Models.Entities;

namespace Pawmart.Services;

public sealed class ProductImageStorageService(
    PawmartDbContext db,
    IWebHostEnvironment environment,
    ILogger<ProductImageStorageService> logger)
{
    public const long MaxImageBytes = 20L * 1024 * 1024;
    private const string PublicPrefix = "/uploads/products/";
    private static readonly Regex ManagedFileName = new(
        @"^[0-9a-f]{32}\.(jpg|png|webp)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private string UploadDirectory => Path.Combine(
        environment.ContentRootPath, "wwwroot", "uploads", "products");

    public async Task<ProductImageResponse> UploadPrimaryAsync(int productId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length <= 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "Invalid image", "Choose a non-empty image file.");
        if (file.Length > MaxImageBytes)
            throw new ApiException(StatusCodes.Status413PayloadTooLarge, "Image is too large", "The maximum image size is 20 MB.");
        if (!await db.Products.AnyAsync(x => x.ProductId == productId, ct))
            throw new ApiException(StatusCodes.Status404NotFound, "Not found", "Product was not found.");

        var extension = await DetectImageExtensionAsync(file, ct);
        var fileName = $"{Guid.NewGuid():N}.{extension}";
        var fullPath = Path.Combine(UploadDirectory, fileName);
        var imageUrl = PublicPrefix + fileName;

        ProductImage image;
        List<string> oldImageUrls;
        try
        {
            Directory.CreateDirectory(UploadDirectory);
            await using (var destination = new FileStream(
                fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await file.CopyToAsync(destination, ct);
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var oldImages = await db.ProductImages.Where(x => x.ProductId == productId).ToListAsync(ct);
            oldImageUrls = oldImages.Select(x => x.ImageUrl).ToList();
            db.ProductImages.RemoveRange(oldImages);
            if (oldImages.Count > 0) await db.SaveChangesAsync(ct);

            image = new ProductImage
            {
                ProductId = productId,
                ImageUrl = imageUrl,
                IsPrimary = true,
                DisplayOrder = 0,
                CreatedAt = DateTime.UtcNow
            };
            db.ProductImages.Add(image);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            DeleteManagedFile(imageUrl);
            throw;
        }

        foreach (var oldUrl in oldImageUrls)
            DeleteManagedFile(oldUrl);

        return new ProductImageResponse(image.ImageId, image.ImageUrl, image.IsPrimary, image.DisplayOrder);
    }

    public void DeleteManagedFile(string imageUrl)
    {
        if (!imageUrl.StartsWith(PublicPrefix, StringComparison.OrdinalIgnoreCase)) return;
        var fileName = imageUrl[PublicPrefix.Length..];
        if (!ManagedFileName.IsMatch(fileName) || fileName != Path.GetFileName(fileName)) return;

        try
        {
            var fullPath = Path.Combine(UploadDirectory, fileName);
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not delete managed product image {ImageUrl}.", imageUrl);
        }
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
        if (read >= 12
            && header.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            && header.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return "webp";

        throw new ApiException(StatusCodes.Status400BadRequest, "Unsupported image", "Use a valid JPEG, PNG, or WebP image.");
    }
}
