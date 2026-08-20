using KitRental.Core.Application.Operations;
using KitRental.Core.Application.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Core.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class ReportingController : CoreApiControllerBase
{
    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpGet("dashboard")]
    public async Task<IActionResult> Get_Dashboard_103([FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetDashboardAsync(cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,Auditor")]
    [HttpGet("audit")]
    public async Task<IActionResult> Get_Audit_104([FromServices] ReportingService service, CancellationToken cancellationToken)
    {
        return Ok((await service.GetAuditTrailAsync(new AuditQuery(null, null, null, null, 1, 100),
                cancellationToken)).Items);
    }

    [Authorize(Roles = "SystemAdmin,Auditor")]
    [HttpGet("audit/search")]
    public async Task<IActionResult> Get_AuditSearch_105(string? action, Guid? actorId, DateTimeOffset? occurredFrom, DateTimeOffset? occurredTo, int? page, int? pageSize, [FromServices] ReportingService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetAuditTrailAsync(new AuditQuery(action, actorId, occurredFrom, occurredTo,
                page ?? 1, pageSize ?? 25), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpGet("reports/inventory.csv")]
    public async Task<IActionResult> Get_ReportsInventoryCsv_106([FromServices] ReportingService service, CancellationToken cancellationToken)
    {
        return File(await service.ExportInventoryCsvAsync(cancellationToken), "text/csv; charset=utf-8", "inventory.csv");
    }

}



