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
    public async Task<IActionResult> GetSupplyNeeds(int? page, int? pageSize, [FromServices] SupplyNeedService service, CancellationToken cancellationToken)
    {
        return Ok((await service.GetAllAsync(cancellationToken)).ToPagedResponse(page, pageSize));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("supply-needs/{id:guid}")]
    public async Task<IActionResult> GetSupplyNeed(Guid id, [FromServices] SupplyNeedService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetAsync(id, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("supply-need-recommendation-refreshes")]
    public async Task<IActionResult> CreateSupplyNeedRecommendationRefresh([FromServices] SupplyNeedService service, CancellationToken cancellationToken)
    {
        return Ok(await service.RefreshRecommendationAsync(cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("supply-needs")]
    public async Task<IActionResult> CreateSupplyNeed(SupplyNeedRequest request, [FromServices] SupplyNeedService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(new CreateSupplyNeedCommand(request.Lines.Select(line =>
                new SupplyNeedLineCommand(line.ComponentId, line.Quantity)).ToArray(), User.GetRequiredUserId()), cancellationToken);
        return Created($"/api/supply-needs/{result.Id}", result);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPut("supply-needs/{id:guid}")]
    public async Task<IActionResult> UpdateSupplyNeed(Guid id, SupplyNeedRequest request, [FromServices] SupplyNeedService service, CancellationToken cancellationToken)
    {
        return Ok(await service.UpdateAsync(
                new UpdateSupplyNeedCommand(id, request.Lines.Select(line =>
                    new SupplyNeedLineCommand(line.ComponentId, line.Quantity)).ToArray(), User.GetRequiredUserId()),
                cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("supply-needs/{id:guid}/completions")]
    public async Task<IActionResult> CreateSupplyNeedCompletion(Guid id, CompleteSupplyNeedRequest request, [FromServices] SupplyNeedService service, CancellationToken cancellationToken)
    {
        return Ok(
                await service.CompleteAsync(new CompleteSupplyNeedCommand(id, request.StorageLocationId,
                    request.Lines.Select(line => new SupplyNeedLineCommand(line.ComponentId, line.Quantity)).ToArray(),
                    User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("supply-needs/{id:guid}/approvals")]
    public async Task<IActionResult> CreateSupplyNeedApproval(Guid id, [FromServices] SupplyNeedService service, CancellationToken cancellationToken)
    {
        return Ok(
                await service.ApproveRecommendationAsync(id, User.GetRequiredUserId(), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpDelete("supply-needs/{id:guid}")]
    public async Task<IActionResult> DeleteSupplyNeed(Guid id, [FromServices] SupplyNeedService service, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, User.GetRequiredUserId(), cancellationToken);
        return NoContent();
    }

}



