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
    public async Task<IActionResult> Get_PhysicalKitsDashboard_36([FromServices] PhysicalKitService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetDashboardAsync(cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("physical-kits")]
    public async Task<IActionResult> Get_PhysicalKits_37([FromServices] PhysicalKitService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetListAsync(cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("physical-kits/lookup")]
    public async Task<IActionResult> Get_PhysicalKitsLookup_38(string identifier, [FromServices] PhysicalKitService service, CancellationToken cancellationToken)
    {
        return Ok(await service.LookupAsync(identifier, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPost("physical-kits/bulk-rent")]
    public async Task<IActionResult> Post_PhysicalKitsBulkRent_39(BulkRentPhysicalKitsRequest request, [FromServices] PhysicalKitService service, CancellationToken cancellationToken)
    {
        return Created("/api/physical-kits", await service.RentManyAsync(new BulkRentPhysicalKitsCommand(
                request.ProductUnitIds, request.CustomerName, request.Email, request.Phone, request.AddressLine,
                request.District, request.City, request.PostalCode, request.StartDate, request.EndDate,
                User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("physical-kits/{id:guid}")]
    public async Task<IActionResult> Get_PhysicalKitsIdGuid_40(Guid id, [FromServices] PhysicalKitService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetDetailAsync(id, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPost("physical-kits/{id:guid}/rent")]
    public async Task<IActionResult> Post_PhysicalKitsIdGuidRent_41(Guid id, RentPhysicalKitRequest request, [FromServices] PhysicalKitService service, CancellationToken cancellationToken)
    {
        return Created($"/api/physical-kits/{id}", await service.RentAsync(new RentPhysicalKitCommand(id,
                request.CustomerName, request.Email, request.Phone, request.AddressLine, request.District, request.City,
                request.PostalCode, request.StartDate, request.EndDate, User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("physical-kits/models")]
    public async Task<IActionResult> Get_PhysicalKitsModels_69([FromServices] PhysicalKitService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetModelSummariesAsync(cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("physical-kits/models/{productModelId:guid}/units")]
    public async Task<IActionResult> Get_PhysicalKitsModelsProductModelIdGuidUnits_70(Guid productModelId, string? filter, int? page, int? pageSize, [FromServices] PhysicalKitService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetModelUnitsAsync(productModelId, filter, page ?? 1, pageSize ?? 20, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("physical-kits/models/{productModelId:guid}/labels")]
    public async Task<IActionResult> Get_PhysicalKitsModelsProductModelIdGuidLabels_71(Guid productModelId, string? filter, [FromServices] PhysicalKitService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetModelUnitsForLabelsAsync(productModelId, filter, cancellationToken));
    }

}



