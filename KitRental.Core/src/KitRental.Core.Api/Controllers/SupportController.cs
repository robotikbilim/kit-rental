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
    public async Task<IActionResult> Post_Faults_94(OpenFaultRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        EnsureCustomerScope(request.CustomerId);
        var result = await service.OpenFaultAsync(
            new OpenFaultCommand(request.CustomerId, request.OrderId, request.AssignmentId, request.ProductUnitId,
                request.Category, request.Severity, request.Description, User.GetRequiredUserId()), cancellationToken);
        return Created($"/api/faults/{result.Id}", result);
    }

    [Authorize]
    [HttpGet("faults")]
    public async Task<IActionResult> Get_Faults_95([FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetFaultTicketsAsync(User.GetCustomerId(), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff,ServiceTechnician,Auditor")]
    [HttpGet("faults/search")]
    public async Task<IActionResult> Get_FaultsSearch_96(string? query, FaultStatus? status, FaultSeverity? severity, DateOnly? openedFrom, DateOnly? openedTo, int page, int pageSize, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetFaultPageAsync(
                new FaultPageQuery(query, status, severity, openedFrom, openedTo, page, pageSize), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,ServiceTechnician")]
    [HttpPost("faults/{ticketId:guid}/status")]
    public async Task<IActionResult> Post_FaultsTicketIdGuidStatus_97(Guid ticketId, FaultStatusRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.ChangeFaultStatusAsync(ticketId, request.Status, User.GetRequiredUserId(), request.Note, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpGet("fault-guides")]
    public async Task<IActionResult> Get_FaultGuides_98([FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetFaultGuideEntriesAsync(false, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPost("fault-guides")]
    public async Task<IActionResult> Post_FaultGuides_99(FaultGuideEntryRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        var result = await service.SaveFaultGuideEntryAsync(new SaveFaultGuideEntryCommand(null, request.Title,
                request.Problem, request.Solution, request.DisplayOrder, request.IsActive, User.GetRequiredUserId()),
                cancellationToken);
        return Created($"/api/fault-guides/{result.Id}", result);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPut("fault-guides/{id:guid}")]
    public async Task<IActionResult> Put_FaultGuidesIdGuid_100(Guid id, FaultGuideEntryRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.SaveFaultGuideEntryAsync(new SaveFaultGuideEntryCommand(id, request.Title,
                request.Problem, request.Solution, request.DisplayOrder, request.IsActive, User.GetRequiredUserId()),
                cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpDelete("fault-guides/{id:guid}")]
    public async Task<IActionResult> Delete_FaultGuidesIdGuid_101(Guid id, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        await service.DeleteFaultGuideEntryAsync(id, User.GetRequiredUserId(), cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("return-inspections")]
    public async Task<IActionResult> Post_ReturnInspections_102(CompleteInspectionRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        var result = await service.CompleteInspectionAsync(
                new CompleteInspectionCommand(request.OrderId, request.ProductUnitId,
                    request.Items.Select(item => new InspectionItemCommand(item.Name, item.IsPresent, item.IsDamaged, item.Note)).ToArray(),
                    request.DamageCharge, request.Outcome, User.GetRequiredUserId()), cancellationToken);
        return Created($"/api/return-inspections/{result.Id}", result);
    }

}



