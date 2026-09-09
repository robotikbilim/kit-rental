using KitRental.Core.Api.Contracts.Requests;
using KitRental.Core.Application.Operations;
using KitRental.Core.Domain.Support;
using KitRental.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Core.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class SupportController : CoreApiControllerBase
{
    [Authorize]
    [HttpPost("faults")]
    public async Task<IActionResult> CreateFault(OpenFaultRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        EnsureCustomerScope(request.CustomerId);
        var result = await service.OpenFaultAsync(
            new OpenFaultCommand(request.CustomerId, request.OrderId, request.AssignmentId, request.ProductUnitId,
                request.Category, request.Severity, request.Description, User.GetRequiredUserId()), cancellationToken);
        return Created($"/api/faults/{result.Id}", result);
    }

    [Authorize]
    [HttpGet("faults")]
    public async Task<IActionResult> GetFaults(string? query, FaultStatus? status, FaultSeverity? severity, DateOnly? openedFrom, DateOnly? openedTo, int? page, int? pageSize, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetFaultPageAsync(
                new FaultPageQuery(query, status, severity, openedFrom, openedTo, page ?? 1, pageSize ?? 20,
                    User.GetCustomerId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,ServiceTechnician")]
    [HttpPost("faults/{ticketId:guid}/status-events")]
    public async Task<IActionResult> CreateFaultStatusEvent(Guid ticketId, FaultStatusRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.ChangeFaultStatusAsync(ticketId, request.Status, User.GetRequiredUserId(), request.Note, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpGet("fault-guides")]
    public async Task<IActionResult> GetFaultGuides(int? page, int? pageSize, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok((await service.GetFaultGuideEntriesAsync(false, cancellationToken)).ToPagedResponse(page, pageSize));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPost("fault-guides")]
    public async Task<IActionResult> CreateFaultGuide(FaultGuideEntryRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        var result = await service.SaveFaultGuideEntryAsync(new SaveFaultGuideEntryCommand(null, request.Title,
                request.Problem, request.Solution, request.DisplayOrder, request.IsActive, User.GetRequiredUserId(),
                request.ProductModelId),
                cancellationToken);
        return Created($"/api/fault-guides/{result.Id}", result);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPut("fault-guides/{id:guid}")]
    public async Task<IActionResult> UpdateFaultGuide(Guid id, FaultGuideEntryRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.SaveFaultGuideEntryAsync(new SaveFaultGuideEntryCommand(id, request.Title,
                request.Problem, request.Solution, request.DisplayOrder, request.IsActive, User.GetRequiredUserId(),
                request.ProductModelId),
                cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpDelete("fault-guides/{id:guid}")]
    public async Task<IActionResult> DeleteFaultGuide(Guid id, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        await service.DeleteFaultGuideEntryAsync(id, User.GetRequiredUserId(), cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("return-inspections")]
    public async Task<IActionResult> CreateReturnInspection(CompleteInspectionRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        var result = await service.CompleteInspectionAsync(
                new CompleteInspectionCommand(request.OrderId, request.ProductUnitId,
                    request.Items.Select(item => new InspectionItemCommand(item.Name, item.IsPresent, item.IsDamaged, item.Note)).ToArray(),
                    request.DamageCharge, request.Outcome, User.GetRequiredUserId()), cancellationToken);
        return Created($"/api/return-inspections/{result.Id}", result);
    }

}



