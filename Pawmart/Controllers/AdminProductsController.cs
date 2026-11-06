using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawmart.Models.Contracts.Catalog;
using Pawmart.Security;
using Pawmart.Services;

namespace Pawmart.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/products")]
public sealed class AdminProductsController(ProductService products, ProductImageStorageService imageStorage) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<ProductResponse>>(StatusCodes.Status200OK)]
    public Task<PagedResponse<ProductResponse>> List([FromQuery] ProductQuery query, CancellationToken ct) =>
        products.ListForAdminAsync(query, ct);

    [HttpGet("summary")]
    [ProducesResponseType<AdminProductSummary>(StatusCodes.Status200OK)]
    public Task<AdminProductSummary> Summary(CancellationToken ct) =>
        products.GetAdminSummaryAsync(ct);

    [HttpGet("{id:int}/catalog-review")]
    [ProducesResponseType<CatalogReviewResponse>(StatusCodes.Status200OK)]
    public Task<CatalogReviewResponse> CatalogReview(int id, CancellationToken ct) =>
        products.GetCatalogReviewAsync(id, ct);

    [HttpPost("{id:int}/images/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(21L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 21L * 1024 * 1024)]
    [ProducesResponseType<ProductImageResponse>(StatusCodes.Status200OK)]
    public Task<ProductImageResponse> UploadImage(int id, IFormFile file, CancellationToken ct) =>
        imageStorage.UploadPrimaryAsync(id, file, ct);
}
