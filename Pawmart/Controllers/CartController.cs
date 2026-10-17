using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawmart.Models.Contracts.Cart;
using Pawmart.Models.Contracts.Orders;
using Pawmart.Security;
using Pawmart.Services;

namespace Pawmart.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Customer)]
[Route("api/cart")]
public sealed class CartController(CartService cart, CheckoutService checkout) : ControllerBase
{
    [HttpGet]
    public Task<CartResponse> Get(CancellationToken ct) => cart.GetAsync(CurrentUserId, ct);

    [HttpPut("items")]
    [ProducesResponseType<CartResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<CartResponse> UpsertItem(CartItemRequest request, CancellationToken ct) =>
        cart.UpsertItemAsync(CurrentUserId, request.ProductId, request.Quantity, ct);

    [HttpDelete("items/{productId:int}")]
    public async Task<IActionResult> RemoveItem(int productId, CancellationToken ct)
    {
        await cart.RemoveItemAsync(CurrentUserId, productId, ct);
        return NoContent();
    }

    [HttpPost("checkout")]
    [ProducesResponseType<OrderSummary>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderSummary>> Checkout(CheckoutRequest request, CancellationToken ct)
    {
        var order = await checkout.CreateOrderAsync(CurrentUserId, request, ct);
        return StatusCode(StatusCodes.Status201Created, order);
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
