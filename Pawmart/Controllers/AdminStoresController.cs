using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawmart.Models.Contracts.Commerce;
using Pawmart.Security;
using Pawmart.Services;

namespace Pawmart.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/stores")]
public sealed class AdminStoresController(StoreService stores) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<StoreResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<StoreResponse>> List(CancellationToken ct) => stores.ListForAdminAsync(ct);
}
