using KitRental.Core.Api.Contracts.Requests;
using KitRental.Core.Application.Inventory;
using KitRental.Core.Domain.Inventory;
using KitRental.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Core.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class InventoryController : CoreApiControllerBase
{
    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPost("product-models")]
    public async Task<IActionResult> CreateProductModel(CreateProductModelRequest request, [FromServices] InventoryService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateModelAsync(
                new CreateProductModelCommand(request.Name, request.Sku, request.Description, request.ImageUrl, User.GetRequiredUserId()),
                cancellationToken);
        return Created($"/api/product-models/{result.Id}", result);
    }

    [Authorize]
    [HttpGet("product-models")]
    public async Task<IActionResult> GetProductModels(int? page, int? pageSize, [FromServices] InventoryService service, CancellationToken cancellationToken)
    {
        return Ok((await service.GetModelsAsync(cancellationToken)).ToPagedResponse(page, pageSize));
    }

    [Authorize]
    [HttpGet("product-models/{productModelId:guid}")]
    public async Task<IActionResult> GetProductModel(Guid productModelId, [FromServices] InventoryService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetModelAsync(productModelId, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPut("product-models/{productModelId:guid}")]
    public async Task<IActionResult> UpdateProductModel(Guid productModelId, UpdateProductModelRequest request, [FromServices] InventoryService service, CancellationToken cancellationToken)
    {
        return Ok(await service.UpdateModelAsync(new UpdateProductModelCommand(productModelId, request.Name, request.Sku,
                request.Description, request.ImageUrl, User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpDelete("product-models/{productModelId:guid}")]
    public async Task<IActionResult> DeleteProductModel(Guid productModelId, [FromServices] InventoryService service, CancellationToken cancellationToken)
    {
        await service.DeleteModelAsync(productModelId, User.GetRequiredUserId(), cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("product-units")]
    public async Task<IActionResult> CreateProductUnit(CreateProductUnitRequest request, [FromServices] InventoryService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateUnitAsync(
                new CreateProductUnitCommand(request.ProductModelId, request.SerialNumber, request.QrCode, User.GetRequiredUserId()),
                cancellationToken);
        return Created($"/api/product-units/{result.Id}", result);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("product-unit-batches")]
    public async Task<IActionResult> CreateProductUnitBatch(CreateProductUnitsRequest request, [FromServices] InventoryService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateUnitsAsync(
                new CreateProductUnitsCommand(request.ProductModelId, request.Quantity, User.GetRequiredUserId()), cancellationToken);
        return Created("/api/product-units", result);
    }

    [Authorize]
    [HttpGet("product-units")]
    public async Task<IActionResult> GetProductUnits(int? page, int? pageSize, [FromServices] InventoryService service, CancellationToken cancellationToken)
    {
        return Ok((await service.GetUnitsAsync(cancellationToken)).ToPagedResponse(page, pageSize));
    }

    [Authorize]
    [HttpGet("inventory")]
    public async Task<IActionResult> GetInventory(string? query, Guid? productModelId, ProductUnitStatus? status, DateOnly? createdFrom, DateOnly? createdTo, string? rentalExpiry, int? page, int? pageSize, [FromServices] InventoryService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetInventoryAsync(query, productModelId,
                status, createdFrom, createdTo, rentalExpiry, page ?? 1, pageSize ?? 20, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPut("product-units/{id:guid}")]
    public async Task<IActionResult> UpdateProductUnit(Guid id, UpdateProductUnitRequest request, [FromServices] InventoryService service, CancellationToken cancellationToken)
    {
        return Ok(await service.UpdateUnitAsync(new UpdateProductUnitCommand(id, request.SerialNumber, request.QrCode,
                User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpDelete("product-units/{id:guid}")]
    public async Task<IActionResult> DeleteProductUnit(Guid id, [FromServices] InventoryService service, CancellationToken cancellationToken)
    {
        await service.DeleteUnitAsync(id, User.GetRequiredUserId(), cancellationToken);
        return NoContent();
    }

}



