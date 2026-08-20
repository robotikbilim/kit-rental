using KitRental.Core.Api.Contracts.Requests;
using KitRental.Core.Application.Operations;
using KitRental.Core.Application.Workshop;
using KitRental.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Core.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class ManufacturingController : CoreApiControllerBase
{
    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("manufacturing/buildable-kits")]
    public async Task<IActionResult> Get_ManufacturingBuildableKits_72([FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetBuildableKitsAsync(null, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpGet("manufacturing/buildable-kits/{productModelId:guid}")]
    public async Task<IActionResult> Get_ManufacturingBuildableKitsProductModelIdGuid_73(Guid productModelId, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        return Ok((await service.GetBuildableKitsAsync(productModelId, cancellationToken)).Single());
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPost("kits")]
    public async Task<IActionResult> Post_Kits_74(CreateKitRequest request, [FromServices] WorkshopService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateKitAsync(new CreateKitCommand(request.Name, request.Sku, request.Description,
                request.ImageUrl, request.BomVersion,
                request.Lines.Select(line => new BillOfMaterialsLineCommand(line.ComponentId, line.Quantity)).ToArray(),
                User.GetRequiredUserId()), cancellationToken);
        return Created($"/api/product-models/{result.Id}", result);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPost("orders/{orderId:guid}/kits")]
    public async Task<IActionResult> Post_OrdersOrderIdGuidKits_89(Guid orderId, CreateOrderKitsRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.CreateAndReserveOrderKitsAsync(
                orderId, request.Lines.Select(line => new OrderKitLineCommand(line.ProductModelId, line.Quantity)).ToArray(),
                request.UseAvailableKits, User.GetRequiredUserId(), cancellationToken, request.RentalCohortId,
                GetActorDisplayName()));
    }

}



