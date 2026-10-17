using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pawmart.Data;
using Pawmart.Middleware;
using Pawmart.Models.Contracts.Commerce;
using Pawmart.Services.Integrations;

namespace Pawmart.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/shipping")]
public sealed class ShippingController(PawmartDbContext db, IShippingCalculator calculator) : ControllerBase
{
    [HttpPost("quote")]
    public async Task<ShippingQuote> Quote(ShippingQuoteRequest request, CancellationToken ct)
    {
        var store = await db.Stores.AsNoTracking().Where(x => x.IsActive && x.StoreType == "STORE").OrderBy(x => x.StoreId).FirstOrDefaultAsync(ct)
            ?? throw new ApiException(503, "No active store location is configured.", "Store location unavailable");
        return await calculator.GetQuoteAsync(store.Latitude, store.Longitude, request.Latitude, request.Longitude, ct);
    }
}
