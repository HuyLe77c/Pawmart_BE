using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawmart.Models.Contracts.Catalog;
using Pawmart.Models.Contracts.Commerce;
using Pawmart.Security;
using Pawmart.Services;

namespace Pawmart.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Customer)]
[Route("api/notifications")]
public sealed class NotificationsController(NotificationService notifications) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<NotificationResponse>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        notifications.ListAsync(CurrentUserId, page, pageSize, ct);

    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(CurrentUserId, id, ct); return NoContent();
    }

    [HttpPut("device-token")]
    public async Task<IActionResult> SetDeviceToken(FcmTokenRequest request, CancellationToken ct)
    {
        await notifications.UpdateTokenAsync(CurrentUserId, request.Token, ct); return NoContent();
    }

    [HttpDelete("device-token")]
    public async Task<IActionResult> ClearDeviceToken(CancellationToken ct)
    {
        await notifications.UpdateTokenAsync(CurrentUserId, null, ct); return NoContent();
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
