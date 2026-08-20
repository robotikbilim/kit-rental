using KitRental.Core.Api.Contracts.Requests;
using KitRental.Core.Application.Abstractions;
using KitRental.Core.Application.CustomerPortal;
using KitRental.Core.Application.Operations;
using KitRental.Core.Application.Rentals;
using KitRental.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Core.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class OperationsController : CoreApiControllerBase
{
    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpGet("email-deliveries")]
    public async Task<IActionResult> Get_EmailDeliveries_7([FromServices] ICoreRepository repository, CancellationToken cancellationToken)
    {
        return Ok(await repository.GetEmailDeliveriesAsync(cancellationToken));
    }

    [Authorize]
    [HttpGet("order-summaries")]
    public async Task<IActionResult> Get_OrderSummaries_24([FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetOrderSummariesAsync(User.GetCustomerId(), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPost("customers")]
    public async Task<IActionResult> Post_Customers_75(CreateCustomerRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateCustomerAsync(
                new CreateCustomerCommand(request.Name, request.Email,
                    new AddressCommand(request.Address.Title, request.Address.ContactName, request.Address.Phone, request.Address.Line1,
                        request.Address.District, request.Address.City, request.Address.PostalCode), User.GetRequiredUserId(),
                    request.AllowedProductModelIds),
                cancellationToken);
        return Created($"/api/customers/{result.Id}", result);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpGet("customers")]
    public async Task<IActionResult> Get_Customers_76([FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetCustomersAsync(cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpGet("customers/{customerId:guid}")]
    public async Task<IActionResult> Get_CustomersCustomerIdGuid_77(Guid customerId, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return await service.GetCustomerAsync(customerId, cancellationToken) is { } customer ? Ok(customer) : NotFound();
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpGet("customers/{customerId:guid}/rental-periods")]
    public async Task<IActionResult> Get_CustomersCustomerIdGuidRentalPeriods_78(Guid customerId, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetRentalCohortsAsync(customerId, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPut("customers/{customerId:guid}")]
    public async Task<IActionResult> Put_CustomersCustomerIdGuid_79(Guid customerId, UpdateCustomerRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.UpdateCustomerAsync(new UpdateCustomerCommand(customerId, request.Name, request.Email,
                request.IsActive, User.GetRequiredUserId(), request.AllowedProductModelIds), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpDelete("customers/{customerId:guid}")]
    public async Task<IActionResult> Delete_CustomersCustomerIdGuid_80(Guid customerId, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.SetCustomerActiveAsync(customerId, false, User.GetRequiredUserId(), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPost("customers/{customerId:guid}/addresses")]
    public async Task<IActionResult> Post_CustomersCustomerIdGuidAddresses_81(Guid customerId, AddressRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Created($"/api/customers/{customerId}/addresses", await service.AddCustomerAddressAsync(
                new CustomerAddressCommand(customerId, null, new AddressCommand(request.Title, request.ContactName, request.Phone,
                    request.Line1, request.District, request.City, request.PostalCode), User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPut("customers/{customerId:guid}/addresses/{addressId:guid}")]
    public async Task<IActionResult> Put_CustomersCustomerIdGuidAddressesAddressIdGuid_82(Guid customerId, Guid addressId, AddressRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.UpdateCustomerAddressAsync(new CustomerAddressCommand(customerId, addressId,
                new AddressCommand(request.Title, request.ContactName, request.Phone, request.Line1, request.District,
                    request.City, request.PostalCode), User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpDelete("customers/{customerId:guid}/addresses/{addressId:guid}")]
    public async Task<IActionResult> Delete_CustomersCustomerIdGuidAddressesAddressIdGuid_83(Guid customerId, Guid addressId, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        await service.RemoveCustomerAddressAsync(customerId, addressId, User.GetRequiredUserId(), cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost("orders")]
    public async Task<IActionResult> Post_Orders_84(CreateOrderRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        EnsureCustomerScope(request.CustomerId);
        var result = await service.CreateOrderAsync(
            new CreateOrderCommand(request.CustomerId, request.AddressId, request.StartDate, request.EndDate,
                request.Lines.Select(line => new OrderLineCommand(line.ProductModelId, line.Quantity)).ToArray(),
                User.GetRequiredUserId()),
            cancellationToken);
        return Created($"/api/orders/{result.Id}", result);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPost("purchase-orders")]
    public async Task<IActionResult> Post_PurchaseOrders_85(CreatePurchaseOrderRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        var result = await service.CreatePurchaseOrderAsync(
                new CreatePurchaseOrderCommand(request.CustomerId, request.AddressId,
                    request.Lines.Select(line => new OrderLineCommand(line.ProductModelId, line.Quantity)).ToArray(),
                    User.GetRequiredUserId()), cancellationToken);
        return Created($"/api/orders/{result.Id}", result);
    }

    [Authorize]
    [HttpGet("orders")]
    public async Task<IActionResult> Get_Orders_86([FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetOrdersAsync(User.GetCustomerId(), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPost("orders/{orderId:guid}/transitions")]
    public async Task<IActionResult> Post_OrdersOrderIdGuidTransitions_87(Guid orderId, OrderTransitionRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.TransitionOrderAsync(orderId, request.Target, User.GetRequiredUserId(), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff,ServiceTechnician,Auditor")]
    [HttpGet("orders/{orderId:guid}/detail")]
    public async Task<IActionResult> Get_OrdersOrderIdGuidDetail_88(Guid orderId, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetOrderDetailAsync(orderId, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPost("rental-assignments")]
    public async Task<IActionResult> Post_RentalAssignments_90(CreateRentalAssignmentRequest request, [FromServices] RentalAssignmentService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(
                new CreateRentalAssignmentCommand(request.OrderLineId, request.CustomerId, request.ProductUnitId, request.StartDate,
                    request.EndDate, User.GetRequiredUserId()), cancellationToken);
        return Created($"/api/rental-assignments/{result.Id}", result);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("shipments")]
    public async Task<IActionResult> Post_Shipments_91(CreateShipmentRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateShipmentAsync(
                new CreateShipmentCommand(request.OrderId, request.FaultTicketId, request.Type, request.Carrier, request.TrackingNumber,
                    User.GetRequiredUserId()), cancellationToken);
        return Created($"/api/shipments/{result.Id}", result);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("shipments/{shipmentId:guid}/events")]
    public async Task<IActionResult> Post_ShipmentsShipmentIdGuidEvents_92(Guid shipmentId, ShipmentEventRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.AddShipmentEventAsync(
                new AddShipmentEventCommand(shipmentId, request.Status, request.OccurredAt, request.Location, request.Description,
                    User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize]
    [HttpGet("orders/{orderId:guid}/shipments")]
    public async Task<IActionResult> Get_OrdersOrderIdGuidShipments_93(Guid orderId, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetShipmentsAsync(orderId, cancellationToken));
    }

}



