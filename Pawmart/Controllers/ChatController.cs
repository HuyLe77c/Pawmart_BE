using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pawmart.Models.Contracts.Commerce;
using Pawmart.Security;
using Pawmart.Services;

namespace Pawmart.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Customer)]
[Route("api/chat")]
public sealed class ChatController(RecommendationService recommendations, ChatMessageService chat) : ControllerBase
{
    [HttpPost("messages")]
    [EnableRateLimiting("customer-chat")]
    public Task<ChatMessageResponse> Message(ChatMessageRequest request, CancellationToken ct) =>
        chat.ReplyAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, request, ct);

    [HttpPost("recommendations")]
    public Task<RecommendationResult> Recommend(RecommendationRequest request, CancellationToken ct) =>
        recommendations.RecommendAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, request.PetId, ct);
}
