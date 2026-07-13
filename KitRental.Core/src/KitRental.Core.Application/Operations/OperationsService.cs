using KitRental.Core.Application.Abstractions;
using KitRental.Core.Application.Common;
using KitRental.Core.Domain.Auditing;
using KitRental.Core.Domain.Customers;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Rentals;
using KitRental.Core.Domain.Returns;
using KitRental.Core.Domain.Support;

namespace KitRental.Core.Application.Operations;

public sealed record AddressCommand(string Title, string ContactName, string Phone, string Line1, string District, string City, string PostalCode);
public sealed record CreateCustomerCommand(string Name, string Email, AddressCommand Address, Guid ActorId);
public sealed record OrderLineCommand(Guid ProductModelId, int Quantity);
public sealed record CreateOrderCommand(Guid CustomerId, Guid AddressId, DateOnly StartDate, DateOnly EndDate, IReadOnlyCollection<OrderLineCommand> Lines, Guid ActorId);
public sealed record CreateShipmentCommand(Guid OrderId, Guid? FaultTicketId, ShipmentType Type, string Carrier, string TrackingNumber, Guid ActorId);
public sealed record AddShipmentEventCommand(Guid ShipmentId, ShipmentStatus Status, DateTimeOffset OccurredAt, string Location, string Description, Guid ActorId);
public sealed record OpenFaultCommand(Guid CustomerId, Guid OrderId, Guid AssignmentId, Guid ProductUnitId, string Category, FaultSeverity Severity, string Description, Guid ActorId);
public sealed record InspectionItemCommand(string Name, bool IsPresent, bool IsDamaged, string Note);
public sealed record CompleteInspectionCommand(Guid OrderId, Guid ProductUnitId, IReadOnlyCollection<InspectionItemCommand> Items, decimal DamageCharge, ProductUnitStatus Outcome, Guid ActorId);
public sealed record DashboardResponse(int Customers, int ProductUnits, int ActiveOrders, int OpenFaults, int UnitsInMaintenance);

public sealed class OperationsService(ICoreRepository repository, TimeProvider timeProvider)
{
    public async Task<Customer> CreateCustomerAsync(CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        var customer = Customer.Create(Guid.NewGuid(), command.Name, command.Email);
        customer.AddAddress(
            command.Address.Title, command.Address.ContactName, command.Address.Phone, command.Address.Line1,
            command.Address.District, command.Address.City, command.Address.PostalCode);
        try
        {
            await repository.AddCustomerAsync(customer, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflictException("customer.email_not_unique", exception.Message);
        }
        await AuditAsync(command.ActorId, nameof(Customer), customer.Id, "Created", null, customer.Name, cancellationToken);
        return customer;
    }

    public Task<IReadOnlyCollection<Customer>> GetCustomersAsync(CancellationToken cancellationToken) =>
        repository.GetCustomersAsync(cancellationToken);

    public async Task<RentalOrder> CreateOrderAsync(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(command.CustomerId, cancellationToken)
            ?? throw new ResourceNotFoundException("Müşteri bulunamadı.");
        if (command.Lines.Count == 0)
            throw new ConflictException("order.lines_required", "Siparişte en az bir ürün satırı bulunmalıdır.");

        foreach (var line in command.Lines)
        {
            if (await repository.GetProductModelAsync(line.ProductModelId, cancellationToken) is null)
                throw new ResourceNotFoundException($"{line.ProductModelId} ürün modeli bulunamadı.");
        }

        var now = timeProvider.GetUtcNow();
        var order = RentalOrder.Create(
            Guid.NewGuid(),
            $"RR-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..20],
            customer.Id,
            new RentalPeriod(command.StartDate, command.EndDate),
            customer.SnapshotAddress(command.AddressId),
            now);
        foreach (var line in command.Lines)
            order.AddLine(line.ProductModelId, line.Quantity);
        order.Submit(command.ActorId, now);
        await repository.AddOrderAsync(order, cancellationToken);
        await AuditAsync(command.ActorId, nameof(RentalOrder), order.Id, "Submitted", null, order.Status.ToString(), cancellationToken);
        return order;
    }

    public Task<IReadOnlyCollection<RentalOrder>> GetOrdersAsync(Guid? customerId, CancellationToken cancellationToken) =>
        repository.GetOrdersAsync(customerId, cancellationToken);

    public async Task<RentalOrder> TransitionOrderAsync(Guid orderId, RentalOrderStatus target, Guid actorId, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(orderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Sipariş bulunamadı.");
        var now = timeProvider.GetUtcNow();
        var previous = order.Status;
        switch (target)
        {
            case RentalOrderStatus.Approved: order.Approve(actorId, now); break;
            case RentalOrderStatus.Preparing: order.StartPreparation(actorId, now); break;
            case RentalOrderStatus.ReadyToShip: order.MarkReadyToShip(actorId, now); break;
            case RentalOrderStatus.AwaitingReturn: order.RequestReturn(actorId, now); break;
            default: throw new ConflictException("order.unsupported_transition", "Bu durum geçişi ilgili süreç üzerinden yapılmalıdır.");
        }
        await AuditAsync(actorId, nameof(RentalOrder), order.Id, "StatusChanged", previous.ToString(), order.Status.ToString(), cancellationToken);
        return order;
    }

    public async Task<Shipment> CreateShipmentAsync(CreateShipmentCommand command, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(command.OrderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Sipariş bulunamadı.");
        var now = timeProvider.GetUtcNow();
        var assignments = await repository.GetAssignmentsForOrderAsync(order.Id, cancellationToken);

        if (command.Type == ShipmentType.Outbound)
        {
            order.Dispatch(command.ActorId, now);
            foreach (var assignment in assignments)
            {
                var unit = await repository.GetProductUnitAsync(assignment.ProductUnitId, cancellationToken);
                unit?.StartPreparation(command.ActorId, now);
                unit?.Dispatch(command.ActorId, now);
            }
        }
        else if (command.Type == ShipmentType.Return)
        {
            order.StartReturnShipment(command.ActorId, now);
            foreach (var assignment in assignments)
            {
                var unit = await repository.GetProductUnitAsync(assignment.ProductUnitId, cancellationToken);
                unit?.StartReturn(command.ActorId, now);
            }
        }

        var shipment = Shipment.Create(Guid.NewGuid(), command.OrderId, command.FaultTicketId, command.Type, command.Carrier, command.TrackingNumber);
        try
        {
            await repository.AddShipmentAsync(shipment, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflictException("shipment.tracking_not_unique", exception.Message);
        }
        await AuditAsync(command.ActorId, nameof(Shipment), shipment.Id, "Created", null, shipment.Status.ToString(), cancellationToken);
        return shipment;
    }

    public async Task<Shipment> AddShipmentEventAsync(AddShipmentEventCommand command, CancellationToken cancellationToken)
    {
        var shipment = await repository.GetShipmentAsync(command.ShipmentId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kargo kaydı bulunamadı.");
        var previousStatus = shipment.Status;
        shipment.AddEvent(command.Status, command.OccurredAt, command.Location, command.Description);

        if (command.Status == ShipmentStatus.Delivered)
        {
            var order = await repository.GetOrderAsync(shipment.OrderId, cancellationToken)
                ?? throw new ResourceNotFoundException("Sipariş bulunamadı.");
            var assignments = await repository.GetAssignmentsForOrderAsync(order.Id, cancellationToken);
            if (shipment.Type == ShipmentType.Outbound)
            {
                order.ConfirmDelivery(command.ActorId, command.OccurredAt);
                order.ActivateRental(command.ActorId, command.OccurredAt);
                foreach (var assignment in assignments)
                    (await repository.GetProductUnitAsync(assignment.ProductUnitId, cancellationToken))?.ConfirmDelivery(command.ActorId, command.OccurredAt);
            }
            else if (shipment.Type == ShipmentType.Return)
            {
                order.ReceiveReturn(command.ActorId, command.OccurredAt);
                foreach (var assignment in assignments)
                    (await repository.GetProductUnitAsync(assignment.ProductUnitId, cancellationToken))?.ReceiveForInspection(command.ActorId, command.OccurredAt);
            }
        }
        await AuditAsync(command.ActorId, nameof(Shipment), shipment.Id, "StatusChanged", previousStatus.ToString(), shipment.Status.ToString(), cancellationToken);
        return shipment;
    }

    public Task<IReadOnlyCollection<Shipment>> GetShipmentsAsync(Guid orderId, CancellationToken cancellationToken) =>
        repository.GetShipmentsAsync(orderId, cancellationToken);

    public async Task<FaultTicket> OpenFaultAsync(OpenFaultCommand command, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(command.OrderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Sipariş bulunamadı.");
        var assignment = await repository.GetRentalAssignmentAsync(command.AssignmentId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kiralama ataması bulunamadı.");
        if (order.CustomerId != command.CustomerId || assignment.CustomerId != command.CustomerId || assignment.ProductUnitId != command.ProductUnitId)
            throw new ForbiddenException("Arıza kaydı müşteri, sipariş ve ürün atamasıyla eşleşmiyor.");

        var now = timeProvider.GetUtcNow();
        var ticket = FaultTicket.Open(
            Guid.NewGuid(), $"FLT-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..21], command.CustomerId, command.OrderId,
            command.AssignmentId, command.ProductUnitId, command.Category, command.Severity, command.Description, now);
        await repository.AddFaultTicketAsync(ticket, cancellationToken);
        await AuditAsync(command.ActorId, nameof(FaultTicket), ticket.Id, "Opened", null, ticket.Status.ToString(), cancellationToken);
        return ticket;
    }

    public Task<IReadOnlyCollection<FaultTicket>> GetFaultTicketsAsync(Guid? customerId, CancellationToken cancellationToken) =>
        repository.GetFaultTicketsAsync(customerId, cancellationToken);

    public async Task<FaultTicket> ChangeFaultStatusAsync(Guid ticketId, FaultStatus status, Guid actorId, string note, CancellationToken cancellationToken)
    {
        var ticket = await repository.GetFaultTicketAsync(ticketId, cancellationToken)
            ?? throw new ResourceNotFoundException("Arıza kaydı bulunamadı.");
        var previous = ticket.Status;
        ticket.ChangeStatus(status, actorId, timeProvider.GetUtcNow(), note);
        await AuditAsync(actorId, nameof(FaultTicket), ticket.Id, "StatusChanged", previous.ToString(), ticket.Status.ToString(), cancellationToken);
        return ticket;
    }

    public async Task<ReturnInspection> CompleteInspectionAsync(CompleteInspectionCommand command, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(command.OrderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Sipariş bulunamadı.");
        var unit = await repository.GetProductUnitAsync(command.ProductUnitId, cancellationToken)
            ?? throw new ResourceNotFoundException("Fiziksel ürün birimi bulunamadı.");
        var now = timeProvider.GetUtcNow();
        var inspection = ReturnInspection.Complete(
            Guid.NewGuid(), command.OrderId, command.ProductUnitId,
            command.Items.Select(item => new InspectionItem(Guid.NewGuid(), item.Name, item.IsPresent, item.IsDamaged, item.Note)).ToArray(),
            command.DamageCharge, command.Outcome, now, command.ActorId);
        unit.CompleteInspection(command.Outcome, command.ActorId, now, "İade kontrolü tamamlandı.");
        order.Complete(command.ActorId, now);
        await repository.AddInspectionAsync(inspection, cancellationToken);
        await AuditAsync(command.ActorId, nameof(ReturnInspection), inspection.Id, "Completed", null, command.Outcome.ToString(), cancellationToken);
        return inspection;
    }

    public async Task<DashboardResponse> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var customers = await repository.GetCustomersAsync(cancellationToken);
        var units = await repository.GetProductUnitsAsync(cancellationToken);
        var orders = await repository.GetOrdersAsync(null, cancellationToken);
        var faults = await repository.GetFaultTicketsAsync(null, cancellationToken);
        return new DashboardResponse(
            customers.Count,
            units.Count,
            orders.Count(order => order.Status is not (RentalOrderStatus.Completed or RentalOrderStatus.Cancelled or RentalOrderStatus.Rejected)),
            faults.Count(ticket => ticket.Status is not (FaultStatus.Resolved or FaultStatus.Closed)),
            units.Count(unit => unit.Status == ProductUnitStatus.InMaintenance));
    }

    private async Task AuditAsync(
        Guid actorId,
        string entityType,
        Guid entityId,
        string action,
        string? previousValue,
        string? newValue,
        CancellationToken cancellationToken)
    {
        await repository.AddAuditEntryAsync(
            new AuditEntry(Guid.NewGuid(), actorId, entityType, entityId, action, previousValue, newValue, timeProvider.GetUtcNow()),
            cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
