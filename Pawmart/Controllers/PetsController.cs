using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawmart.Models.Contracts.Catalog;
using Pawmart.Security;
using Pawmart.Services;

namespace Pawmart.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Customer)]
[Route("api/pets")]
public sealed class PetsController(PetService pets, PetImageStorageService petImages) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<PetResponse>> List(CancellationToken ct) => pets.ListAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, ct);

    [HttpPost]
    public async Task<ActionResult<PetResponse>> Create(PetWriteRequest request, CancellationToken ct)
    {
        var pet = await pets.CreateAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, request, ct);
        return StatusCode(StatusCodes.Status201Created, pet);
    }

    [HttpPost("{id:int}/image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(21L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 21L * 1024 * 1024)]
    [ProducesResponseType<PetResponse>(StatusCodes.Status200OK)]
    public Task<PetResponse> UploadImage(int id, IFormFile file, CancellationToken ct) =>
        petImages.UploadAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, id, file, ct);

    [HttpPut("{id:int}")]
    public Task<PetResponse> Update(int id, PetWriteRequest request, CancellationToken ct) =>
        pets.UpdateAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, id, request, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await pets.DeleteAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, id, ct);
        return NoContent();
    }
}
