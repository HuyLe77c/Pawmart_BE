using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawmart.Models.Contracts.Commerce;
using Pawmart.Security;
using Pawmart.Services;

namespace Pawmart.Controllers;

[ApiController]
[Route("api/stores")]
public sealed class StoresController(StoreService stores) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public Task<IReadOnlyList<StoreResponse>> List(CancellationToken ct) => stores.ListActiveAsync(ct);

    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [HttpPost]
    public async Task<ActionResult<StoreResponse>> Create(StoreWriteRequest request, CancellationToken ct)
    {
        var result = await stores.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [HttpPut("{id:int}")]
    public Task<StoreResponse> Update(int id, StoreWriteRequest request, CancellationToken ct) => stores.UpdateAsync(id, request, ct);

    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [HttpPatch("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, SetActiveRequest request, CancellationToken ct)
    {
        await stores.SetActiveAsync(id, request.IsActive, ct); return NoContent();
    }
}
