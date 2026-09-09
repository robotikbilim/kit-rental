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
    public async Task<IActionResult> GetDashboard([FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetDashboardAsync(cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPost("dashboard/kit-location-geocoding-jobs")]
    public async Task<IActionResult> CreateKitLocationGeocodingJob(
        [FromServices] KitLocationGeocodingService service,
        [FromServices] IKitLocationGeocodingQueue queue,
        CancellationToken cancellationToken)
    {
        var plan = await service.GetMissingCoordinateQueuePlanAsync(cancellationToken);
        if (!plan.IsConfigured || plan.CandidateCount == 0)
            return Accepted(plan);

        var enqueued = 0;
        foreach (var candidateId in await service.GetMissingCoordinateCandidateIdsAsync(cancellationToken))
        {
            if (queue.TryEnqueue(candidateId))
                enqueued++;
        }

        return Accepted(plan with { EnqueuedCount = enqueued });
    }

    [Authorize(Roles = "SystemAdmin,Auditor")]
    [HttpGet("audit")]
    public async Task<IActionResult> GetAudit([FromServices] ReportingService service, CancellationToken cancellationToken)
    {
        return Ok((await service.GetAuditTrailAsync(new AuditQuery(null, null, null, null, 1, 100),
                cancellationToken)).Items);
    }

    [Authorize(Roles = "SystemAdmin,Auditor")]
    [HttpGet("audit-entries")]
    public async Task<IActionResult> GetAuditEntries(string? action, Guid? actorId, DateTimeOffset? occurredFrom, DateTimeOffset? occurredTo, int? page, int? pageSize, [FromServices] ReportingService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetAuditTrailAsync(new AuditQuery(action, actorId, occurredFrom, occurredTo,
                page ?? 1, pageSize ?? 25), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpGet("reports/inventory.csv")]
    public async Task<IActionResult> GetInventoryReport([FromServices] ReportingService service, CancellationToken cancellationToken)
    {
        return File(await service.ExportInventoryCsvAsync(cancellationToken), "text/csv; charset=utf-8", "inventory.csv");
    }

}



