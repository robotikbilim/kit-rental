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
    public async Task<IActionResult> CreateComponent(CreateComponentRequest request, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateComponentAsync(
                new CreateComponentCommand(request.Name, request.Sku, request.UnitOfMeasure, request.MinimumStock, request.ImageUrl,
                    request.DefaultStorageLocationId, User.GetRequiredUserId(), request.InitialStock),
                cancellationToken);
        return Created($"/api/components/{result.Id}", result);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("components")]
    public async Task<IActionResult> GetComponents(bool? lowStockOnly, int? page, int? pageSize, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok((await service.GetComponentsAsync(lowStockOnly ?? false, cancellationToken)).ToPagedResponse(page, pageSize));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("components/low-stock")]
    public async Task<IActionResult> GetLowStockComponents(int? page, int? pageSize, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok((await service.GetComponentsAsync(true, cancellationToken)).ToPagedResponse(page, pageSize));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("component-suggestions")]
    public async Task<IActionResult> GetComponentSuggestions(string? query, int? limit, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok(await service.SearchComponentsAsync(query, limit ?? 8, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("components/{componentId:guid}/locator")]
    public async Task<IActionResult> GetComponentLocator(Guid componentId, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetComponentLocatorAsync(componentId, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("components/{componentId:guid}/stock-adjustments")]
    public async Task<IActionResult> CreateComponentStockAdjustment(Guid componentId, AdjustComponentStockRequest request, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok(await service.AdjustStockAsync(
                new AdjustComponentStockCommand(componentId, request.Change, User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("storage-locations")]
    public async Task<IActionResult> CreateStorageLocation(CreateStorageLocationRequest request, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateLocationAsync(
                new CreateStorageLocationCommand(request.Code, request.Warehouse, request.Aisle, request.Rack, request.Shelf,
                    User.GetRequiredUserId(), request.IsDefaultForNewComponents), cancellationToken);
        return Created($"/api/storage-locations/{result.Id}", result);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("storage-locations")]
    public async Task<IActionResult> GetStorageLocations(int? page, int? pageSize, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok((await service.GetLocationsAsync(cancellationToken)).ToPagedResponse(page, pageSize));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPut("storage-locations/{id:guid}")]
    public async Task<IActionResult> UpdateStorageLocation(Guid id, CreateStorageLocationRequest request, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok(
                await service.UpdateLocationAsync(new UpdateStorageLocationCommand(id, request.Code, request.Warehouse,
                    request.Aisle, request.Rack, request.Shelf, User.GetRequiredUserId(),
                    request.IsDefaultForNewComponents), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpDelete("storage-locations/{id:guid}")]
    public async Task<IActionResult> DeleteStorageLocation(Guid id, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        await service.DeleteLocationAsync(id, User.GetRequiredUserId(), cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("component-stock/receipts")]
    public async Task<IActionResult> CreateComponentStockReceipt(RecordComponentStockRequest request, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Created("/api/component-stock/movements", await service.ReceiveAsync(
                new RecordStockCommand(request.ComponentId, request.StorageLocationId, request.Quantity, request.Reference,
                    User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("component-stock/consumptions")]
    public async Task<IActionResult> CreateComponentStockConsumption(RecordComponentStockRequest request, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Created("/api/component-stock/movements", await service.ConsumeAsync(
                new RecordStockCommand(request.ComponentId, request.StorageLocationId, request.Quantity, request.Reference,
                    User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("component-stock/transfers")]
    public async Task<IActionResult> CreateComponentStockTransfer(TransferComponentStockRequest request, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok(await service.TransferAsync(
                new TransferStockCommand(request.ComponentId, request.FromStorageLocationId, request.ToStorageLocationId,
                    request.Quantity, request.Reference, User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("component-stock")]
    public async Task<IActionResult> GetComponentStock(Guid? componentId, Guid? locationId, int? page, int? pageSize, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok((await service.GetStocksAsync(componentId, locationId, cancellationToken)).ToPagedResponse(page, pageSize));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("component-stock/movements")]
    public async Task<IActionResult> GetComponentStockMovements(Guid? componentId, int? page, int? pageSize, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok((await service.GetMovementsAsync(componentId, cancellationToken)).ToPagedResponse(page, pageSize));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("product-models/{productModelId:guid}/bom")]
    public async Task<IActionResult> CreateProductModelBom(Guid productModelId, CreateBillOfMaterialsRequest request, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateBomAsync(new CreateBillOfMaterialsCommand(productModelId, request.Version,
                request.Lines.Select(line => new BillOfMaterialsLineCommand(line.ComponentId, line.Quantity)).ToArray(),
                User.GetRequiredUserId()), cancellationToken);
        return Created($"/api/product-models/{productModelId}/bom", result);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("product-models/{productModelId:guid}/bom")]
    public async Task<IActionResult> GetProductModelBom(Guid productModelId, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        var bom = await service.GetActiveBomAsync(productModelId, cancellationToken);
        return bom is null ? NoContent() : Ok(bom);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPut("components/{componentId:guid}")]
    public async Task<IActionResult> UpdateComponent(Guid componentId, UpdateComponentRequest request, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok(await service.UpdateComponentAsync(new UpdateComponentCommand(componentId, request.Name, request.Sku,
                request.UnitOfMeasure, request.MinimumStock, request.ImageUrl, request.DefaultStorageLocationId,
                User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpDelete("components/{componentId:guid}")]
    public async Task<IActionResult> DeleteComponent(Guid componentId, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        await service.DeleteComponentAsync(componentId, User.GetRequiredUserId(), cancellationToken);
        return NoContent();
    }

}



