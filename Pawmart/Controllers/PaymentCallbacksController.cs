using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawmart.Services;

namespace Pawmart.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/payments/callback")]
public sealed class PaymentCallbacksController(PaymentService payments) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        var values = Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);
            foreach (var pair in form) values[pair.Key] = pair.Value.ToString();
        }
        await payments.ApplyVerifiedCallbackAsync(values, ct);
        return Ok(new { accepted = true });
    }
}
