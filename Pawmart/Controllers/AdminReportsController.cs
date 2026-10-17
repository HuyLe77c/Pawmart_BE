using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawmart.Models.Contracts.Commerce;
using Pawmart.Security;
using Pawmart.Services;

namespace Pawmart.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/reports")]
public sealed class AdminReportsController(AdminReportService reports) : ControllerBase
{
    [HttpGet("revenue")]
    public Task<RevenueReport> Revenue([FromQuery] RevenueReportQuery query, CancellationToken ct) => reports.GetRevenueAsync(query, ct);

    [HttpGet("orders-by-status")]
    public Task<IReadOnlyList<OrderStatusCount>> OrdersByStatus(CancellationToken ct) => reports.GetOrderCountsAsync(ct);

    [HttpGet("low-stock")]
    public Task<IReadOnlyList<LowStockProduct>> LowStock(CancellationToken ct) => reports.GetLowStockAsync(ct);
}
