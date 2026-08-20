using KitRental.Core.Api.Contracts.Requests;
using KitRental.Core.Application.Procurement;
using KitRental.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Core.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class SupplyNeedsController : CoreApiControllerBase
{
    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("supply-needs")]
    public async Task<IActionResult> Get_SupplyNeeds_61([FromServices] SupplyNeedService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetAllAsync(cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("supply-needs/{id:guid}")]
    public async Task<IActionResult> Get_SupplyNeedsIdGuid_62(Guid id, [FromServices] SupplyNeedService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetAsync(id, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("supply-needs/refresh-recommendation")]
    public async Task<IActionResult> Post_SupplyNeedsRefreshRecommendation_63([FromServices] SupplyNeedService service, CancellationToken cancellationToken)
    {
        return Ok(await service.RefreshRecommendationAsync(cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("supply-needs")]
    public async Task<IActionResult> Post_SupplyNeeds_64(SupplyNeedRequest request, [FromServices] SupplyNeedService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(new CreateSupplyNeedCommand(request.Lines.Select(line =>
                new SupplyNeedLineCommand(line.ComponentId, line.Quantity)).ToArray(), User.GetRequiredUserId()), cancellationToken);
        return Created($"/api/supply-needs/{result.Id}", result);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPut("supply-needs/{id:guid}")]
    public async Task<IActionResult> Put_SupplyNeedsIdGuid_65(Guid id, SupplyNeedRequest request, [FromServices] SupplyNeedService service, CancellationToken cancellationToken)
    {
        return Ok(await service.UpdateAsync(
                new UpdateSupplyNeedCommand(id, request.Lines.Select(line =>
                    new SupplyNeedLineCommand(line.ComponentId, line.Quantity)).ToArray(), User.GetRequiredUserId()),
                cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("supply-needs/{id:guid}/complete")]
    public async Task<IActionResult> Post_SupplyNeedsIdGuidComplete_66(Guid id, CompleteSupplyNeedRequest request, [FromServices] SupplyNeedService service, CancellationToken cancellationToken)
    {
        return Ok(
                await service.CompleteAsync(new CompleteSupplyNeedCommand(id, request.StorageLocationId,
                    request.Lines.Select(line => new SupplyNeedLineCommand(line.ComponentId, line.Quantity)).ToArray(),
                    User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("supply-needs/{id:guid}/approve")]
    public async Task<IActionResult> Post_SupplyNeedsIdGuidApprove_67(Guid id, [FromServices] SupplyNeedService service, CancellationToken cancellationToken)
    {
        return Ok(
                await service.ApproveRecommendationAsync(id, User.GetRequiredUserId(), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpDelete("supply-needs/{id:guid}")]
    public async Task<IActionResult> Delete_SupplyNeedsIdGuid_68(Guid id, [FromServices] SupplyNeedService service, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, User.GetRequiredUserId(), cancellationToken);
        return NoContent();
    }

}



