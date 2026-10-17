using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawmart.Models.Contracts.Catalog;
using Pawmart.Models.Contracts.Orders;
using Pawmart.Security;
using Pawmart.Services;

namespace Pawmart.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Customer)]
[Route("api/orders")]
public sealed class OrdersController(OrderService orders) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<OrderListItemResponse>> List([FromQuery] OrderListQuery query, CancellationToken ct) =>
        orders.ListForCustomerAsync(CurrentUserId, query, ct);

    [HttpGet("{orderId}")]
    public Task<OrderDetailResponse> Get(string orderId, CancellationToken ct) => orders.GetForCustomerAsync(CurrentUserId, orderId, ct);

    [HttpPost("{orderId}/cancel")]
    public async Task<IActionResult> Cancel(string orderId, CancellationToken ct)
    {
        await orders.CancelAsync(CurrentUserId, orderId, ct);
        return NoContent();
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
