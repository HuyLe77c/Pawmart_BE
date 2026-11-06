using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawmart.Models.Contracts.Catalog;
using Pawmart.Models.Contracts.Orders;
using Pawmart.Security;
using Pawmart.Services;

namespace Pawmart.Controllers;

[ApiController]
[Route("api/orders/{orderId}/review")]
[Authorize(Policy = AuthorizationPolicies.Customer)]
public sealed class OrderReviewsController(OrderReviewService reviews) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<OrderReviewResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<OrderReviewResponse>> Create(string orderId,
        CreateOrderReviewRequest request, CancellationToken ct)
    {
        var review = await reviews.CreateAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, orderId, request, ct);
        return StatusCode(StatusCodes.Status201Created, review);
    }
}

[ApiController]
[Route("api/reviews")]
public sealed class PublicReviewsController(OrderReviewService reviews) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public Task<PublicOrderReviewsResponse> List(CancellationToken ct) => reviews.ListPublicAsync(ct);
}

[ApiController]
[Route("api/admin/reviews")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class AdminReviewsController(OrderReviewService reviews) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<OrderReviewResponse>> List([FromQuery] int page = 1,
        [FromQuery] int pageSize = 30, CancellationToken ct = default) =>
        reviews.ListForAdminAsync(page, pageSize, ct);

    [HttpPut("{reviewId:int}/reply")]
    public Task<OrderReviewResponse> Reply(int reviewId, ReplyOrderReviewRequest request, CancellationToken ct) =>
        reviews.ReplyAsync(reviewId, request, ct);

    [HttpPut("{reviewId:int}/visibility")]
    public Task<OrderReviewResponse> SetVisibility(int reviewId,
        SetOrderReviewVisibilityRequest request, CancellationToken ct) =>
        reviews.SetVisibilityAsync(reviewId, request, ct);
}
