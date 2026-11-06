using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawmart.Models.Contracts.Catalog;
using Pawmart.Models.Contracts.Orders;
using Pawmart.Security;
using Pawmart.Services;

namespace Pawmart.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/orders")]
public sealed class AdminOrdersController(OrderService orders, BankQrService bankQr) : ControllerBase
{
    [HttpPost("{orderId}/payment/confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ConfirmPayment(string orderId, ConfirmPaymentRequest request, CancellationToken ct)
    {
        await bankQr.ConfirmReceiptAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, orderId, request, ct);
        return NoContent();
    }

    [HttpGet]
    public Task<PagedResponse<OrderListItemResponse>> List([FromQuery] OrderListQuery query, CancellationToken ct) => orders.ListForAdminAsync(query, ct);

    [HttpGet("{orderId}")]
    public Task<OrderDetailResponse> Get(string orderId, CancellationToken ct) => orders.GetForAdminAsync(orderId, ct);

    [HttpPut("{orderId}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetStatus(string orderId, OrderStatusRequest request, CancellationToken ct)
    {
        await orders.TransitionStatusAsync(orderId, request.Status, ct);
        return NoContent();
    }
}
