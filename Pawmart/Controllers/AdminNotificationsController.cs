using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawmart.Models.Contracts.Commerce;
using Pawmart.Security;
using Pawmart.Services;

namespace Pawmart.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/notifications")]
public sealed class AdminNotificationsController(NotificationService notifications) : ControllerBase
{
    [HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast(NotificationCreateRequest request, CancellationToken ct)
    {
        return Ok(await notifications.BroadcastAsync(request, ct));
    }
}
