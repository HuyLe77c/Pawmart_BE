using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawmart.Models.Contracts.Catalog;
using Pawmart.Models.Contracts.Commerce;
using Pawmart.Security;
using Pawmart.Services;

namespace Pawmart.Controllers;

[ApiController]
[Route("api/promotions")]
public sealed class PromotionsController(PromotionService promotions) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public Task<IReadOnlyList<PromotionResponse>> List(CancellationToken ct) => promotions.ListActiveAsync(ct);

    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [HttpGet("admin")]
    public Task<PagedResponse<PromotionResponse>> ListAdmin([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => promotions.ListAdminAsync(page, pageSize, ct);

    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [HttpPost]
    public async Task<ActionResult<PromotionResponse>> Create(PromotionWriteRequest request, CancellationToken ct)
    {
        var result = await promotions.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [HttpPut("{id:int}")]
    public Task<PromotionResponse> Update(int id, PromotionWriteRequest request, CancellationToken ct) => promotions.UpdateAsync(id, request, ct);

    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [HttpPatch("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, SetActiveRequest request, CancellationToken ct)
    {
        await promotions.SetActiveAsync(id, request.IsActive, ct); return NoContent();
    }
}
