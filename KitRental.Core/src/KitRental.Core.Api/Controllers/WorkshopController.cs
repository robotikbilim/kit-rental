using KitRental.Core.Api.Contracts.Requests;
using KitRental.Core.Application.Workshop;
using KitRental.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Core.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class WorkshopController : CoreApiControllerBase
{
    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("components")]
    public async Task<IActionResult> Post_Components_42(CreateComponentRequest request, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateComponentAsync(
                new CreateComponentCommand(request.Name, request.Sku, request.UnitOfMeasure, request.MinimumStock, request.ImageUrl,
                    request.DefaultStorageLocationId, User.GetRequiredUserId(), request.InitialStock),
                cancellationToken);
        return Created($"/api/components/{result.Id}", result);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("components")]
    public async Task<IActionResult> Get_Components_43(bool? lowStockOnly, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetComponentsAsync(lowStockOnly ?? false, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("components/low-stock")]
    public async Task<IActionResult> Get_ComponentsLowStock_44([FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetComponentsAsync(true, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("components/search")]
    public async Task<IActionResult> Get_ComponentsSearch_45(string? query, int? limit, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok(await service.SearchComponentsAsync(query, limit ?? 8, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("components/{componentId:guid}/locator")]
    public async Task<IActionResult> Get_ComponentsComponentIdGuidLocator_46(Guid componentId, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetComponentLocatorAsync(componentId, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("components/{componentId:guid}/stock-adjustments")]
    public async Task<IActionResult> Post_ComponentsComponentIdGuidStockAdjustments_47(Guid componentId, AdjustComponentStockRequest request, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok(await service.AdjustStockAsync(
                new AdjustComponentStockCommand(componentId, request.Change, User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("storage-locations")]
    public async Task<IActionResult> Post_StorageLocations_48(CreateStorageLocationRequest request, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateLocationAsync(
                new CreateStorageLocationCommand(request.Code, request.Warehouse, request.Aisle, request.Rack, request.Shelf,
                    User.GetRequiredUserId(), request.IsDefaultForNewComponents), cancellationToken);
        return Created($"/api/storage-locations/{result.Id}", result);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("storage-locations")]
    public async Task<IActionResult> Get_StorageLocations_49([FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetLocationsAsync(cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPut("storage-locations/{id:guid}")]
    public async Task<IActionResult> Put_StorageLocationsIdGuid_50(Guid id, CreateStorageLocationRequest request, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok(
                await service.UpdateLocationAsync(new UpdateStorageLocationCommand(id, request.Code, request.Warehouse,
                    request.Aisle, request.Rack, request.Shelf, User.GetRequiredUserId(),
                    request.IsDefaultForNewComponents), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpDelete("storage-locations/{id:guid}")]
    public async Task<IActionResult> Delete_StorageLocationsIdGuid_51(Guid id, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        await service.DeleteLocationAsync(id, User.GetRequiredUserId(), cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("component-stock/receipts")]
    public async Task<IActionResult> Post_ComponentStockReceipts_52(RecordComponentStockRequest request, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Created("/api/component-stock/movements", await service.ReceiveAsync(
                new RecordStockCommand(request.ComponentId, request.StorageLocationId, request.Quantity, request.Reference,
                    User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("component-stock/consumptions")]
    public async Task<IActionResult> Post_ComponentStockConsumptions_53(RecordComponentStockRequest request, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Created("/api/component-stock/movements", await service.ConsumeAsync(
                new RecordStockCommand(request.ComponentId, request.StorageLocationId, request.Quantity, request.Reference,
                    User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("component-stock/transfers")]
    public async Task<IActionResult> Post_ComponentStockTransfers_54(TransferComponentStockRequest request, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok(await service.TransferAsync(
                new TransferStockCommand(request.ComponentId, request.FromStorageLocationId, request.ToStorageLocationId,
                    request.Quantity, request.Reference, User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("component-stock")]
    public async Task<IActionResult> Get_ComponentStock_55(Guid? componentId, Guid? locationId, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetStocksAsync(componentId, locationId, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("component-stock/movements")]
    public async Task<IActionResult> Get_ComponentStockMovements_56(Guid? componentId, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetMovementsAsync(componentId, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("product-models/{productModelId:guid}/bom")]
    public async Task<IActionResult> Post_ProductModelsProductModelIdGuidBom_57(Guid productModelId, CreateBillOfMaterialsRequest request, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateBomAsync(new CreateBillOfMaterialsCommand(productModelId, request.Version,
                request.Lines.Select(line => new BillOfMaterialsLineCommand(line.ComponentId, line.Quantity)).ToArray(),
                User.GetRequiredUserId()), cancellationToken);
        return Created($"/api/product-models/{productModelId}/bom", result);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("product-models/{productModelId:guid}/bom")]
    public async Task<IActionResult> Get_ProductModelsProductModelIdGuidBom_58(Guid productModelId, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        var bom = await service.GetActiveBomAsync(productModelId, cancellationToken);
        return bom is null ? NoContent() : Ok(bom);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPut("components/{componentId:guid}")]
    public async Task<IActionResult> Put_ComponentsComponentIdGuid_59(Guid componentId, UpdateComponentRequest request, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok(await service.UpdateComponentAsync(new UpdateComponentCommand(componentId, request.Name, request.Sku,
                request.UnitOfMeasure, request.MinimumStock, request.ImageUrl, request.DefaultStorageLocationId,
                User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpDelete("components/{componentId:guid}")]
    public async Task<IActionResult> Delete_ComponentsComponentIdGuid_60(Guid componentId, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        await service.DeleteComponentAsync(componentId, User.GetRequiredUserId(), cancellationToken);
        return NoContent();
    }

}



