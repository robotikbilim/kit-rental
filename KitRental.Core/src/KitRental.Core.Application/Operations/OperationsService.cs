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
using KitRental.Core.Application.Inventory;

namespace KitRental.Core.Application.Operations;

public sealed record AddressCommand(string Title, string ContactName, string Phone, string Line1, string District, string City, string PostalCode);
public sealed record CreateCustomerCommand(string Name, string Email, AddressCommand Address, Guid ActorId);
public sealed record UpdateCustomerCommand(Guid CustomerId, string Name, string Email, bool IsActive, Guid ActorId);
public sealed record CustomerAddressCommand(Guid CustomerId, Guid? AddressId, AddressCommand Address, Guid ActorId);
public sealed record OrderLineCommand(Guid ProductModelId, int Quantity);
public sealed record CreateOrderCommand(Guid CustomerId, Guid AddressId, DateOnly StartDate, DateOnly EndDate, IReadOnlyCollection<OrderLineCommand> Lines, Guid ActorId);
public sealed record CreateShipmentCommand(Guid OrderId, Guid? FaultTicketId, ShipmentType Type, string Carrier, string TrackingNumber, Guid ActorId);
public sealed record AddShipmentEventCommand(Guid ShipmentId, ShipmentStatus Status, DateTimeOffset OccurredAt, string Location, string Description, Guid ActorId);
public sealed record OpenFaultCommand(Guid CustomerId, Guid OrderId, Guid AssignmentId, Guid ProductUnitId, string Category, FaultSeverity Severity, string Description, Guid ActorId);
public sealed record InspectionItemCommand(string Name, bool IsPresent, bool IsDamaged, string Note);
public sealed record CompleteInspectionCommand(Guid OrderId, Guid ProductUnitId, IReadOnlyCollection<InspectionItemCommand> Items, decimal DamageCharge, ProductUnitStatus Outcome, Guid ActorId);
public sealed record FaultPageQuery(string? Query, FaultStatus? Status, FaultSeverity? Severity,
    DateOnly? OpenedFrom, DateOnly? OpenedTo, int Page = 1, int PageSize = 20);
public sealed record FaultListItemResponse(Guid Id, string Number, Guid CustomerId, string CustomerName,
    string ReporterName, string ReporterPhone, string Category, FaultSeverity Severity, string Description,
    FaultStatus Status, DateTimeOffset OpenedAt);
public sealed record FaultPageResponse(int Page, int PageSize, int TotalCount, int TotalPages,
    IReadOnlyCollection<FaultListItemResponse> Items);
public sealed record OrderKitResponse(Guid ProductUnitId, Guid AssignmentId, Guid ProductModelId,
    string SerialNumber, ProductUnitStatus Status);
public sealed record OrderKitPreparationResponse(Guid OrderId, int CreatedCount,
    IReadOnlyCollection<OrderKitResponse> Kits);
public sealed record OrderKitLineCommand(Guid ProductModelId, int Quantity);
public sealed record OrderDetailLineResponse(Guid Id, Guid ProductModelId, string ProductName, string ProductSku,
    int Quantity, int CreatedKitCount);
public sealed record OrderDetailKitResponse(Guid Id, Guid OrderLineId, Guid ProductModelId, string ProductName,
    string ProductSku, string SerialNumber, string QrCode, ProductUnitStatus Status);
public sealed record OrderDetailResponse(Guid Id, string OrderNumber, string CustomerName, RentalOrderStatus Status,
    DateOnly StartDate, DateOnly EndDate, DateTimeOffset CreatedAt,
    IReadOnlyCollection<OrderDetailLineResponse> Lines, IReadOnlyCollection<OrderDetailKitResponse> Kits);
public sealed record DashboardResponse(
    int Customers,
    int ProductUnits,
    int RentedKits,
    int AvailableKits,
    int FaultyKits,
    int RepairedAwaitingShipment,
    int PreparingKits,
    int KitsInTransit,
    int KitsUnderInspection,
    int UnitsInMaintenance,
    int ActiveOrders,
    int OrdersAwaitingApproval,
    int OverdueOrders);

public sealed class OperationsService(
    ICoreRepository repository,
    TimeProvider timeProvider,
    ProductUnitStockConsumptionPlanner stockConsumptionPlanner)
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

    public Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken) =>
        repository.GetCustomerAsync(customerId, cancellationToken);

    public async Task<Customer> UpdateCustomerAsync(UpdateCustomerCommand command, CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(command.CustomerId, cancellationToken)
            ?? throw new ResourceNotFoundException("Müşteri bulunamadı.");
        var duplicate = await repository.FindCustomerByEmailAsync(command.Email, cancellationToken);
        if (duplicate is not null && duplicate.Id != customer.Id)
            throw new ConflictException("customer.email_not_unique", "Müşteri e-posta adresi benzersiz olmalıdır.");
        var previous = $"{customer.Name}|{customer.Email}|{customer.IsActive}";
        customer.Update(command.Name, command.Email);
        customer.SetActive(command.IsActive);
        await repository.SaveChangesAsync(cancellationToken);
        await AuditAsync(command.ActorId, nameof(Customer), customer.Id, "Updated", previous,
            $"{customer.Name}|{customer.Email}|{customer.IsActive}", cancellationToken);
        return customer;
    }

    public async Task<Customer> SetCustomerActiveAsync(Guid customerId, bool isActive, Guid actorId,
        CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(customerId, cancellationToken)
            ?? throw new ResourceNotFoundException("Müşteri bulunamadı.");
        customer.SetActive(isActive);
        await repository.SaveChangesAsync(cancellationToken);
        await AuditAsync(actorId, nameof(Customer), customer.Id, isActive ? "Activated" : "Deactivated",
            (!isActive).ToString(), isActive.ToString(), cancellationToken);
        return customer;
    }

    public async Task<Address> AddCustomerAddressAsync(CustomerAddressCommand command, CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(command.CustomerId, cancellationToken)
            ?? throw new ResourceNotFoundException("Müşteri bulunamadı.");
        var address = customer.AddAddress(command.Address.Title, command.Address.ContactName, command.Address.Phone,
            command.Address.Line1, command.Address.District, command.Address.City, command.Address.PostalCode);
        await repository.SaveChangesAsync(cancellationToken);
        await AuditAsync(command.ActorId, nameof(Customer), customer.Id, "AddressAdded", null, address.Title, cancellationToken);
        return address;
    }

    public async Task<Address> UpdateCustomerAddressAsync(CustomerAddressCommand command, CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(command.CustomerId, cancellationToken)
            ?? throw new ResourceNotFoundException("Müşteri bulunamadı.");
        var address = customer.UpdateAddress(command.AddressId ?? Guid.Empty, command.Address.Title,
            command.Address.ContactName, command.Address.Phone, command.Address.Line1, command.Address.District,
            command.Address.City, command.Address.PostalCode);
        await repository.SaveChangesAsync(cancellationToken);
        await AuditAsync(command.ActorId, nameof(Customer), customer.Id, "AddressUpdated", null, address.Title, cancellationToken);
        return address;
    }

    public async Task RemoveCustomerAddressAsync(Guid customerId, Guid addressId, Guid actorId,
        CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(customerId, cancellationToken)
            ?? throw new ResourceNotFoundException("Müşteri bulunamadı.");
        customer.RemoveAddress(addressId);
        await repository.SaveChangesAsync(cancellationToken);
        await AuditAsync(actorId, nameof(Customer), customer.Id, "AddressRemoved", addressId.ToString(), null, cancellationToken);
    }

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

    public async Task<OrderDetailResponse> GetOrderDetailAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(orderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Sipariş bulunamadı.");
        var customer = await repository.GetCustomerAsync(order.CustomerId, cancellationToken);
        var models = (await repository.GetProductModelsAsync(cancellationToken)).ToDictionary(item => item.Id);
        var assignments = await repository.GetAssignmentsForOrderAsync(order.Id, cancellationToken);
        var assignmentCounts = assignments.GroupBy(item => item.OrderLineId)
            .ToDictionary(group => group.Key, group => group.Count());
        var lines = order.Lines.Select(line => new OrderDetailLineResponse(line.Id, line.ProductModelId,
            models.TryGetValue(line.ProductModelId, out var model) ? model.Name : "Eğitim kiti",
            models.TryGetValue(line.ProductModelId, out model) ? model.Sku : "-", line.Quantity,
            assignmentCounts.GetValueOrDefault(line.Id))).ToArray();
        var kits = new List<OrderDetailKitResponse>();
        foreach (var assignment in assignments)
        {
            var unit = await repository.GetProductUnitAsync(assignment.ProductUnitId, cancellationToken);
            if (unit is null) continue;
            models.TryGetValue(unit.ProductModelId, out var model);
            kits.Add(new OrderDetailKitResponse(unit.Id, assignment.OrderLineId, unit.ProductModelId,
                model?.Name ?? "Eğitim kiti", model?.Sku ?? "-", unit.SerialNumber, unit.QrCode, unit.Status));
        }
        return new OrderDetailResponse(order.Id, order.OrderNumber, customer?.Name ?? "Müşteri", order.Status,
            order.Period.StartDate, order.Period.EndDate, order.CreatedAt, lines,
            kits.OrderBy(item => item.ProductName).ThenBy(item => item.SerialNumber).ToArray());
    }

    public async Task<OrderKitPreparationResponse> CreateAndReserveOrderKitsAsync(Guid orderId,
        IReadOnlyCollection<OrderKitLineCommand> requestedLines, Guid actorId,
        CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(orderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Sipariş bulunamadı.");
        if (order.Status != RentalOrderStatus.Approved)
            throw new ConflictException("order.not_approved", "Fiziksel kitler yalnızca onaylanmış sipariş için oluşturulabilir.");

        var existingAssignments = await repository.GetAssignmentsForOrderAsync(order.Id, cancellationToken);
        if (existingAssignments.Count > 0)
            throw new ConflictException("order.kits_already_created", "Bu siparişin fiziksel kitleri daha önce oluşturulmuş.");
        var lines = requestedLines
            .Where(line => line.ProductModelId != Guid.Empty && line.Quantity > 0)
            .GroupBy(line => line.ProductModelId)
            .Select(group => new OrderKitLineCommand(group.Key, group.Sum(line => line.Quantity)))
            .ToArray();
        if (lines.Length == 0)
            throw new ConflictException("order.lines_required", "En az bir kit ve adet seçilmelidir.");
        if (lines.Sum(line => line.Quantity) > 200)
            throw new ConflictException("order.kit_limit_exceeded", "Tek siparişte en fazla 200 fiziksel kit oluşturulabilir.");
        var models = new Dictionary<Guid, ProductModel>();
        foreach (var requestedLine in lines)
        {
            var model = await repository.GetProductModelAsync(requestedLine.ProductModelId, cancellationToken)
                ?? throw new ResourceNotFoundException("Seçilen eğitim kitlerinden biri bulunamadı.");
            models[model.Id] = model;
        }
        order.ReplaceLines(lines.Select(line => (line.ProductModelId, line.Quantity)).ToArray());

        var now = timeProvider.GetUtcNow();
        var units = new List<ProductUnit>();
        var assignments = new List<RentalAssignment>();
        foreach (var line in order.Lines)
        {
            for (var index = 0; index < line.Quantity; index++)
            {
                var serialNumber = $"ORD-{now:yyyyMMdd}-{Guid.NewGuid():N}".ToUpperInvariant();
                var unit = ProductUnit.Create(Guid.NewGuid(), line.ProductModelId, serialNumber,
                    $"KITRENTAL:{serialNumber}", actorId, now);
                units.Add(unit);
                assignments.Add(RentalAssignment.Create(Guid.NewGuid(), line.Id, order.CustomerId, unit.Id,
                    order.Period, now, actorId));
            }
        }

        var stockMovements = await stockConsumptionPlanner.CreateMovementsAsync(
            units.GroupBy(unit => unit.ProductModelId)
                .Select(group => new ProductUnitProduction(models[group.Key], group.Select(unit => unit.Id).ToArray()))
                .ToArray(), actorId, now, cancellationToken);
        var creationAudits = units.Select(unit => new AuditEntry(Guid.NewGuid(), actorId, nameof(ProductUnit), unit.Id,
            "CreatedForOrder", null, order.OrderNumber, now)).ToArray();
        try
        {
            await repository.AddProductUnitsWithStockConsumptionAsync(
                units, stockMovements, creationAudits, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflictException("product_unit.identifier_not_unique", exception.Message);
        }

        if (!await repository.TryCreateReservationsAsync(units, assignments, actorId, now, cancellationToken))
        {
            foreach (var unit in units)
                await repository.RemoveProductUnitAsync(unit, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            throw new ConflictException("order.kit_reservation_failed", "Sipariş kitleri rezerve edilemedi.");
        }

        await AuditAsync(actorId, nameof(RentalOrder), order.Id, "OrderKitsCreated", null,
            units.Count.ToString(), cancellationToken);
        var assignmentByUnit = assignments.ToDictionary(item => item.ProductUnitId);
        return new OrderKitPreparationResponse(order.Id, units.Count, units.Select(unit =>
            new OrderKitResponse(unit.Id, assignmentByUnit[unit.Id].Id, unit.ProductModelId,
                unit.SerialNumber, unit.Status)).ToArray());
    }

    public async Task<RentalOrder> TransitionOrderAsync(Guid orderId, RentalOrderStatus target, Guid actorId, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(orderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Sipariş bulunamadı.");
        var now = timeProvider.GetUtcNow();
        var previous = order.Status;
        var assignments = await repository.GetAssignmentsForOrderAsync(order.Id, cancellationToken);
        if (target is RentalOrderStatus.Preparing or RentalOrderStatus.OutboundInTransit or RentalOrderStatus.Delivered)
        {
            var requestedKitCount = order.Lines.Sum(line => line.Quantity);
            if (assignments.Count != requestedKitCount)
                throw new ConflictException("order.kits_incomplete",
                    $"Siparişin {requestedKitCount} fiziksel kitinin tamamı oluşturulup rezerve edilmelidir.");
        }
        switch (target)
        {
            case RentalOrderStatus.Approved: order.Approve(actorId, now); break;
            case RentalOrderStatus.Preparing:
                order.StartPreparation(actorId, now);
                foreach (var assignment in assignments)
                {
                    var unit = await repository.GetProductUnitAsync(assignment.ProductUnitId, cancellationToken);
                    if (unit?.Status == ProductUnitStatus.Reserved)
                        unit.StartPreparation(actorId, now);
                    if (assignment.Status == RentalAssignmentStatus.Reserved)
                        assignment.Activate();
                }
                break;
            case RentalOrderStatus.ReadyToShip: order.MarkReadyToShip(actorId, now); break;
            case RentalOrderStatus.OutboundInTransit:
                if (order.Status == RentalOrderStatus.Preparing)
                    order.MarkReadyToShip(actorId, now);
                order.Dispatch(actorId, now);
                foreach (var assignment in assignments)
                {
                    var unit = await repository.GetProductUnitAsync(assignment.ProductUnitId, cancellationToken);
                    if (unit?.Status == ProductUnitStatus.Reserved)
                        unit.StartPreparation(actorId, now);
                    if (unit?.Status == ProductUnitStatus.Preparing)
                        unit.Dispatch(actorId, now);
                }
                break;
            case RentalOrderStatus.Delivered:
                order.ConfirmDelivery(actorId, now);
                foreach (var assignment in assignments)
                {
                    var unit = await repository.GetProductUnitAsync(assignment.ProductUnitId, cancellationToken);
                    if (unit?.Status == ProductUnitStatus.OutboundInTransit)
                        unit.ConfirmDelivery(actorId, now);
                    if (assignment.Status == RentalAssignmentStatus.Reserved)
                        assignment.Activate();
                }
                order.LockAfterDelivery(actorId, now);
                break;
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
                if (unit?.Status == ProductUnitStatus.Reserved)
                    unit.StartPreparation(command.ActorId, now);
                if (unit?.Status == ProductUnitStatus.Preparing)
                    unit.Dispatch(command.ActorId, now);
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

    public async Task<FaultPageResponse> GetFaultPageAsync(FaultPageQuery query,
        CancellationToken cancellationToken)
    {
        var customers = (await repository.GetCustomersAsync(cancellationToken)).ToDictionary(item => item.Id);
        var orders = (await repository.GetOrdersAsync(null, cancellationToken)).ToDictionary(item => item.Id);
        var items = (await repository.GetFaultTicketsAsync(null, cancellationToken)).Select(ticket =>
        {
            customers.TryGetValue(ticket.CustomerId, out var customer);
            orders.TryGetValue(ticket.OrderId, out var order);
            var reporterName = order?.DeliveryAddress.ContactName
                ?? customer?.Addresses.FirstOrDefault()?.ContactName
                ?? customer?.Name
                ?? "-";
            var reporterPhone = order?.DeliveryAddress.Phone
                ?? customer?.Addresses.FirstOrDefault()?.Phone
                ?? "-";
            return new FaultListItemResponse(ticket.Id, ticket.Number, ticket.CustomerId,
                customer?.Name ?? "Müşteri", reporterName, reporterPhone, ticket.Category, ticket.Severity,
                ticket.Description, ticket.Status, ticket.OpenedAt);
        });

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            var term = query.Query.Trim();
            items = items.Where(item =>
                item.Number.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.ReporterName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.ReporterPhone.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.Category.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.Description.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        if (query.Status.HasValue)
            items = items.Where(item => item.Status == query.Status.Value);
        if (query.Severity.HasValue)
            items = items.Where(item => item.Severity == query.Severity.Value);
        if (query.OpenedFrom.HasValue)
            items = items.Where(item => DateOnly.FromDateTime(item.OpenedAt.Date) >= query.OpenedFrom.Value);
        if (query.OpenedTo.HasValue)
            items = items.Where(item => DateOnly.FromDateTime(item.OpenedAt.Date) <= query.OpenedTo.Value);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 10, 100);
        var ordered = items.OrderByDescending(item => item.OpenedAt).ToArray();
        var totalPages = Math.Max(1, (int)Math.Ceiling(ordered.Length / (double)pageSize));
        page = Math.Min(page, totalPages);
        return new FaultPageResponse(page, pageSize, ordered.Length, totalPages,
            ordered.Skip((page - 1) * pageSize).Take(pageSize).ToArray());
    }

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
        var openFaultUnitIds = faults
            .Where(ticket => ticket.Status is not (FaultStatus.Resolved or FaultStatus.Closed))
            .Select(ticket => ticket.ProductUnitId)
            .ToHashSet();
        var repairedUnitIds = faults
            .Where(ticket => ticket.Status is FaultStatus.Resolved or FaultStatus.Closed)
            .Select(ticket => ticket.ProductUnitId)
            .ToHashSet();
        var faultyUnitIds = units
            .Where(unit => unit.Status is ProductUnitStatus.InMaintenance or ProductUnitStatus.Quarantined)
            .Select(unit => unit.Id)
            .Concat(openFaultUnitIds)
            .ToHashSet();

        return new DashboardResponse(
            customers.Count,
            units.Count,
            units.Count(unit => unit.Status == ProductUnitStatus.WithCustomer),
            units.Count(unit => unit.Status == ProductUnitStatus.Available && !faultyUnitIds.Contains(unit.Id)),
            faultyUnitIds.Count,
            units.Count(unit => repairedUnitIds.Contains(unit.Id) && !openFaultUnitIds.Contains(unit.Id) &&
                unit.Status is ProductUnitStatus.Reserved or ProductUnitStatus.Preparing),
            units.Count(unit => unit.Status == ProductUnitStatus.Preparing),
            units.Count(unit => unit.Status is ProductUnitStatus.OutboundInTransit or ProductUnitStatus.ReturnInTransit),
            units.Count(unit => unit.Status == ProductUnitStatus.UnderInspection),
            units.Count(unit => unit.Status == ProductUnitStatus.InMaintenance),
            orders.Count(order => order.Status is not (RentalOrderStatus.Completed or RentalOrderStatus.Cancelled or RentalOrderStatus.Rejected)),
            orders.Count(order => order.Status == RentalOrderStatus.PendingApproval),
            orders.Count(order => order.Status == RentalOrderStatus.Overdue));
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
