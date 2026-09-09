using KitRental.Core.Api.Contracts.Requests;
using KitRental.Core.Application.PhysicalKits;
using KitRental.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Core.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class PhysicalKitsController : CoreApiControllerBase
{
    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("physical-kits/dashboard")]
    public async Task<IActionResult> GetPhysicalKitDashboard([FromServices] PhysicalKitService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetDashboardAsync(cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("physical-kits")]
    public async Task<IActionResult> GetPhysicalKits(int? page, int? pageSize, [FromServices] PhysicalKitService service, CancellationToken cancellationToken)
    {
        return Ok((await service.GetListAsync(cancellationToken)).ToPagedResponse(page, pageSize));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("physical-kits/lookup")]
    public async Task<IActionResult> LookupPhysicalKit(string identifier, [FromServices] PhysicalKitService service, CancellationToken cancellationToken)
    {
        return Ok(await service.LookupAsync(identifier, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPost("physical-kit-rental-batches")]
    public async Task<IActionResult> CreatePhysicalKitRentalBatch(BulkRentPhysicalKitsRequest request, [FromServices] PhysicalKitService service, CancellationToken cancellationToken)
    {
        return Created("/api/physical-kits", await service.RentManyAsync(new BulkRentPhysicalKitsCommand(
                request.ProductUnitIds, request.CustomerName, request.Email, request.Phone, request.AddressLine,
                request.PostalCode, request.StartDate, request.EndDate,
                User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("physical-kits/{id:guid}")]
    public async Task<IActionResult> GetPhysicalKit(Guid id, [FromServices] PhysicalKitService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetDetailAsync(id, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPost("physical-kits/{id:guid}/rentals")]
    public async Task<IActionResult> CreatePhysicalKitRental(Guid id, RentPhysicalKitRequest request, [FromServices] PhysicalKitService service, CancellationToken cancellationToken)
    {
        return Created($"/api/physical-kits/{id}", await service.RentAsync(new RentPhysicalKitCommand(id,
                request.CustomerName, request.Email, request.Phone, request.AddressLine, request.PostalCode,
                request.StartDate, request.EndDate, User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("physical-kits/models")]
    public async Task<IActionResult> GetPhysicalKitModels(int? page, int? pageSize, [FromServices] PhysicalKitService service, CancellationToken cancellationToken)
    {
        return Ok((await service.GetModelSummariesAsync(cancellationToken)).ToPagedResponse(page, pageSize));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("physical-kits/models/{productModelId:guid}/units")]
    public async Task<IActionResult> GetPhysicalKitModelUnits(Guid productModelId, string? filter, int? page, int? pageSize, [FromServices] PhysicalKitService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetModelUnitsAsync(productModelId, filter, page ?? 1, pageSize ?? 20, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("physical-kits/models/{productModelId:guid}/labels")]
    public async Task<IActionResult> GetPhysicalKitModelLabels(Guid productModelId, string? filter, int? page, int? pageSize, [FromServices] PhysicalKitService service, CancellationToken cancellationToken)
    {
        return Ok((await service.GetModelUnitsForLabelsAsync(productModelId, filter, cancellationToken)).ToPagedResponse(page, pageSize));
    }

}



