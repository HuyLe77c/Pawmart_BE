using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawmart.Services;

namespace Pawmart.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/payments/sepay-webhook")]
public sealed class SePayWebhooksController(SePayWebhookService service) : ControllerBase
{
    [HttpPost]
    [Consumes("application/json")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> Receive([FromBody] SePayWebhookPayload payload, CancellationToken ct)
    {
        if (!service.IsConfigured) return StatusCode(503, new { success = false });
        if (!service.IsAuthorized(Request.Headers.Authorization)) return Unauthorized(new { success = false });
        var result = await service.ProcessAsync(payload, ct);
        if (result.Status == "INVALID") return BadRequest(new { success = false });
        return StatusCode(201, new { success = true, status = result.Status });
    }
}
