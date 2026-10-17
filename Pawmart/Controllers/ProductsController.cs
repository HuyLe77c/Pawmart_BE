using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawmart.Models.Contracts.Catalog;
using Pawmart.Models.Contracts.Commerce;
using Pawmart.Security;
using Pawmart.Services;

namespace Pawmart.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(ProductService products) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType<PagedResponse<ProductResponse>>(StatusCodes.Status200OK)]
    public Task<PagedResponse<ProductResponse>> Search([FromQuery] ProductQuery query, CancellationToken ct) => products.SearchAsync(query, ct);

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    public Task<ProductResponse> Get(int id, CancellationToken ct) => products.GetByIdAsync(id, ct);

    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [HttpPost]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProductResponse>> Create(ProductWriteRequest request, CancellationToken ct)
    {
        var product = await products.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = product.ProductId }, product);
    }

    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [HttpPut("{id:int}")]
    public Task<ProductResponse> Update(int id, ProductWriteRequest request, CancellationToken ct) => products.UpdateAsync(id, request, ct);

    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [HttpPatch("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, [FromBody] SetActiveRequest request, CancellationToken ct)
    {
        await products.SetActiveAsync(id, request.IsActive, ct);
        return NoContent();
    }

}
