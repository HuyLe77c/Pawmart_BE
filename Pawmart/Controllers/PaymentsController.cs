using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawmart.Security;
using Pawmart.Services;
using Pawmart.Services.Integrations;

namespace Pawmart.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Customer)]
[Route("api/orders/{orderId}/payment")]
public sealed class PaymentsController(PaymentService payments) : ControllerBase
{
    [HttpPost("initiate")]
    public Task<PaymentStartResult> Initiate(string orderId, CancellationToken ct) =>
        payments.StartAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, orderId, ct);
}
