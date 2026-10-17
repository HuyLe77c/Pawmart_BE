using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawmart.Models.Contracts.Catalog;
using Pawmart.Models.Contracts.Commerce;
using Pawmart.Security;
using Pawmart.Services;

namespace Pawmart.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(CategoryService categories) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public Task<IReadOnlyList<CategoryResponse>> List(CancellationToken ct) => categories.ListAsync(ct);

    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(CategoryWriteRequest request, CancellationToken ct)
    {
        var category = await categories.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, category);
    }

    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [HttpPut("{id:int}")]
    public Task<CategoryResponse> Update(int id, CategoryWriteRequest request, CancellationToken ct) => categories.UpdateAsync(id, request, ct);

    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [HttpPatch("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, [FromBody] SetActiveRequest request, CancellationToken ct)
    {
        await categories.SetActiveAsync(id, request.IsActive, ct);
        return NoContent();
    }
}
