using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawmart.Models.Contracts.Commerce;
using Pawmart.Security;
using Pawmart.Services;

namespace Pawmart.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Customer)]
[Route("api/chat")]
public sealed class ChatController(RecommendationService recommendations) : ControllerBase
{
    [HttpPost("recommendations")]
    public Task<RecommendationResult> Recommend(RecommendationRequest request, CancellationToken ct) =>
        recommendations.RecommendAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, request.PetId, ct);
}
