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
    public async Task<IActionResult> GetEmailDeliveries(int? page, int? pageSize, [FromServices] ICoreRepository repository, CancellationToken cancellationToken)
    {
        return Ok((await repository.GetEmailDeliveriesAsync(cancellationToken)).ToPagedResponse(page, pageSize));
    }

    [Authorize]
    [HttpGet("order-summaries")]
    public async Task<IActionResult> GetOrderSummaries(int? page, int? pageSize, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        return Ok((await service.GetOrderSummariesAsync(User.GetCustomerId(), cancellationToken)).ToPagedResponse(page, pageSize));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPost("customers")]
    public async Task<IActionResult> CreateCustomer(CreateCustomerRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateCustomerAsync(
                new CreateCustomerCommand(request.Name, request.Email,
                    new AddressCommand(request.Address.Title, request.Address.ContactName, request.Address.Phone, request.Address.Line1,
                        request.Address.PostalCode), User.GetRequiredUserId(),
                    request.AllowedProductModelIds),
                cancellationToken);
        return Created($"/api/customers/{result.Id}", result);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers(int? page, int? pageSize, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok((await service.GetCustomersAsync(cancellationToken)).ToPagedResponse(page, pageSize));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpGet("customers/{customerId:guid}")]
    public async Task<IActionResult> GetCustomer(Guid customerId, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return await service.GetCustomerAsync(customerId, cancellationToken) is { } customer ? Ok(customer) : NotFound();
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpGet("customers/{customerId:guid}/rental-periods")]
    public async Task<IActionResult> GetCustomerRentalPeriods(Guid customerId, int? page, int? pageSize, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        return Ok((await service.GetRentalCohortsAsync(customerId, cancellationToken)).ToPagedResponse(page, pageSize));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPut("customers/{customerId:guid}")]
    public async Task<IActionResult> UpdateCustomer(Guid customerId, UpdateCustomerRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.UpdateCustomerAsync(new UpdateCustomerCommand(customerId, request.Name, request.Email,
                request.IsActive, User.GetRequiredUserId(), request.AllowedProductModelIds), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpDelete("customers/{customerId:guid}")]
    public async Task<IActionResult> DeleteCustomer(Guid customerId, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.SetCustomerActiveAsync(customerId, false, User.GetRequiredUserId(), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPost("customers/{customerId:guid}/addresses")]
    public async Task<IActionResult> CreateCustomerAddress(Guid customerId, AddressRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Created($"/api/customers/{customerId}/addresses", await service.AddCustomerAddressAsync(
                new CustomerAddressCommand(customerId, null, new AddressCommand(request.Title, request.ContactName, request.Phone,
                    request.Line1, request.PostalCode), User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPut("customers/{customerId:guid}/addresses/{addressId:guid}")]
    public async Task<IActionResult> UpdateCustomerAddress(Guid customerId, Guid addressId, AddressRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.UpdateCustomerAddressAsync(new CustomerAddressCommand(customerId, addressId,
                new AddressCommand(request.Title, request.ContactName, request.Phone, request.Line1,
                    request.PostalCode), User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpDelete("customers/{customerId:guid}/addresses/{addressId:guid}")]
    public async Task<IActionResult> DeleteCustomerAddress(Guid customerId, Guid addressId, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        await service.RemoveCustomerAddressAsync(customerId, addressId, User.GetRequiredUserId(), cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        EnsureCustomerScope(request.CustomerId);
        var result = await service.CreateStudentAddressOrderAsync(
            new CreateStudentAddressOrderCommand(request.CustomerId, request.ProductModelId,
                request.StartDate, request.EndDate,
                request.Students.Select(student =>
                    new CreateStudentAddressOrderStudentCommand(student.FullName, student.GuardianPhone)).ToArray(),
                User.GetRequiredUserId()),
            cancellationToken);
        return Created($"/api/orders/{result.Id}", result);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPost("purchase-orders")]
    public async Task<IActionResult> CreatePurchaseOrder(CreatePurchaseOrderRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        var result = await service.CreatePurchaseOrderAsync(
                new CreatePurchaseOrderCommand(request.CustomerId, request.AddressId,
                    request.Lines.Select(line => new OrderLineCommand(line.ProductModelId, line.Quantity)).ToArray(),
                    User.GetRequiredUserId()), cancellationToken);
        return Created($"/api/orders/{result.Id}", result);
    }

    [Authorize]
    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders(int? page, int? pageSize, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok((await service.GetOrdersAsync(User.GetCustomerId(), cancellationToken)).ToPagedResponse(page, pageSize));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPost("orders/{orderId:guid}/status-transitions")]
    public async Task<IActionResult> CreateOrderStatusTransition(Guid orderId, OrderTransitionRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.TransitionOrderAsync(orderId, request.Target, User.GetRequiredUserId(), cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff,ServiceTechnician,Auditor")]
    [HttpGet("orders/{orderId:guid}")]
    public async Task<IActionResult> GetOrder(Guid orderId, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetOrderDetailAsync(orderId, cancellationToken));
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager")]
    [HttpPost("rental-assignments")]
    public async Task<IActionResult> CreateRentalAssignment(CreateRentalAssignmentRequest request, [FromServices] RentalAssignmentService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(
                new CreateRentalAssignmentCommand(request.OrderLineId, request.CustomerId, request.ProductUnitId, request.StartDate,
                    request.EndDate, User.GetRequiredUserId()), cancellationToken);
        return Created($"/api/rental-assignments/{result.Id}", result);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("shipments")]
    public async Task<IActionResult> CreateShipment(CreateShipmentRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateShipmentAsync(
                new CreateShipmentCommand(request.OrderId, request.FaultTicketId, request.Type, request.Carrier, request.TrackingNumber,
                    User.GetRequiredUserId()), cancellationToken);
        return Created($"/api/shipments/{result.Id}", result);
    }

    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("shipments/{shipmentId:guid}/events")]
    public async Task<IActionResult> CreateShipmentEvent(Guid shipmentId, ShipmentEventRequest request, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.AddShipmentEventAsync(
                new AddShipmentEventCommand(shipmentId, request.Status, request.OccurredAt, request.Location, request.Description,
                    User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize]
    [HttpGet("orders/{orderId:guid}/shipments")]
    public async Task<IActionResult> GetOrderShipments(Guid orderId, int? page, int? pageSize, [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok((await service.GetShipmentsAsync(orderId, cancellationToken)).ToPagedResponse(page, pageSize));
    }

}



