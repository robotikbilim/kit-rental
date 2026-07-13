using KitRental.Core.Application.Abstractions;
using KitRental.Core.Application.Common;
using KitRental.Core.Application.Operations;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Rentals;
using KitRental.Core.Domain.Support;

namespace KitRental.Core.Application.CustomerPortal;

public sealed class CustomerPortalService(ICoreRepository repository, OperationsService operationsService)
{
    public async Task<CustomerPortalResponse> GetOverviewAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(customerId, cancellationToken)
            ?? throw new ResourceNotFoundException("Müşteri hesabı bulunamadı.");
        var productModels = await repository.GetProductModelsAsync(cancellationToken);
        var modelLookup = productModels.ToDictionary(item => item.Id);
        var orders = await repository.GetOrdersAsync(customerId, cancellationToken);
        var kits = new List<PortalKitResponse>();
        var orderResponses = new List<PortalOrderResponse>();
        var customerFaults = await repository.GetFaultTicketsAsync(customerId, cancellationToken);

        foreach (var order in orders)
        {
            orderResponses.Add(new PortalOrderResponse(order.Id, order.OrderNumber, customer.Id, customer.Name,
                order.Status, order.Period.StartDate, order.Period.EndDate, order.CreatedAt,
                order.Lines.Select(line => new PortalOrderLineResponse(line.ProductModelId,
                    modelLookup.TryGetValue(line.ProductModelId, out var lineModel) ? lineModel.Name : "Eğitim kiti",
                    modelLookup.TryGetValue(line.ProductModelId, out lineModel) ? lineModel.Sku : "-", line.Quantity)).ToArray()));

            var lineIds = order.Lines.Select(line => line.Id).ToHashSet();
            foreach (var assignment in await repository.GetAssignmentsForOrderAsync(order.Id, cancellationToken))
            {
                if (!lineIds.Contains(assignment.OrderLineId) || assignment.Status == RentalAssignmentStatus.Cancelled)
                    continue;
                var unit = await repository.GetProductUnitAsync(assignment.ProductUnitId, cancellationToken);
                if (unit is null || !modelLookup.TryGetValue(unit.ProductModelId, out var model))
                    continue;
                var openFaults = customerFaults.Count(ticket =>
                    ticket.ProductUnitId == unit.Id && ticket.Status is not (FaultStatus.Resolved or FaultStatus.Closed));
                kits.Add(new PortalKitResponse(unit.Id, assignment.Id, order.Id, order.OrderNumber, model.Name, model.Sku,
                    model.ImageUrl, unit.SerialNumber, unit.Status, assignment.Status, assignment.Period.StartDate,
                    assignment.Period.EndDate, openFaults));
            }
        }

        var faults = await MapFaultsAsync(customerId, modelLookup, cancellationToken);
        return new CustomerPortalResponse(customer.Name, customer.Email,
            kits.Count(item => item.AssignmentStatus == RentalAssignmentStatus.Active),
            orders.Count(item => item.Status == RentalOrderStatus.PendingApproval),
            faults.Count(item => item.Status is not (FaultStatus.Resolved or FaultStatus.Closed)),
            kits.OrderByDescending(item => item.AssignmentStatus).ThenBy(item => item.KitName).ToArray(),
            orderResponses, faults,
            customer.Addresses.Select(item => new PortalAddressResponse(item.Id, item.Title, item.ContactName, item.Phone,
                item.Line1, item.District, item.City, item.PostalCode)).ToArray(),
            productModels.Select(item => new PortalProductModelResponse(item.Id, item.Name, item.Sku, item.Description,
                item.ImageUrl)).ToArray());
    }

    public async Task<IReadOnlyCollection<PortalOrderResponse>> GetOrderSummariesAsync(Guid? customerId,
        CancellationToken cancellationToken)
    {
        var customers = (await repository.GetCustomersAsync(cancellationToken)).ToDictionary(item => item.Id);
        var models = (await repository.GetProductModelsAsync(cancellationToken)).ToDictionary(item => item.Id);
        return (await repository.GetOrdersAsync(customerId, cancellationToken)).Select(order =>
            new PortalOrderResponse(order.Id, order.OrderNumber, order.CustomerId,
                customers.TryGetValue(order.CustomerId, out var customer) ? customer.Name : "Müşteri",
                order.Status, order.Period.StartDate, order.Period.EndDate, order.CreatedAt,
                order.Lines.Select(line => new PortalOrderLineResponse(line.ProductModelId,
                    models.TryGetValue(line.ProductModelId, out var model) ? model.Name : "Eğitim kiti",
                    models.TryGetValue(line.ProductModelId, out model) ? model.Sku : "-", line.Quantity)).ToArray()))
            .ToArray();
    }

    public Task<RentalOrder> CreateRentalRequestAsync(CreatePortalRentalRequestCommand command,
        CancellationToken cancellationToken) => operationsService.CreateOrderAsync(new CreateOrderCommand(
        command.CustomerId, command.AddressId, command.StartDate, command.EndDate,
        command.Lines.Select(line => new OrderLineCommand(line.ProductModelId, line.Quantity)).ToArray(),
        command.ActorId), cancellationToken);

    public async Task<FaultTicket> OpenFaultAsync(OpenPortalFaultCommand command, CancellationToken cancellationToken)
    {
        var assignment = await repository.GetRentalAssignmentAsync(command.AssignmentId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kiralama kaydı bulunamadı.");
        if (assignment.CustomerId != command.CustomerId || assignment.Status != RentalAssignmentStatus.Active)
            throw new ForbiddenException("Yalnızca hesabınıza ait aktif kiralamalar için arıza kaydı açabilirsiniz.");
        var order = await repository.FindOrderByLineIdAsync(assignment.OrderLineId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kiralama siparişi bulunamadı.");
        return await operationsService.OpenFaultAsync(new OpenFaultCommand(command.CustomerId, order.Id, assignment.Id,
            assignment.ProductUnitId, command.Category, command.Severity, command.Description, command.ActorId),
            cancellationToken);
    }

    private async Task<IReadOnlyCollection<PortalFaultResponse>> MapFaultsAsync(Guid customerId,
        IReadOnlyDictionary<Guid, ProductModel> models, CancellationToken cancellationToken)
    {
        var result = new List<PortalFaultResponse>();
        foreach (var ticket in await repository.GetFaultTicketsAsync(customerId, cancellationToken))
        {
            var unit = await repository.GetProductUnitAsync(ticket.ProductUnitId, cancellationToken);
            var modelName = unit is not null && models.TryGetValue(unit.ProductModelId, out var model)
                ? model.Name : "Eğitim kiti";
            var shipments = (await repository.GetShipmentsAsync(ticket.OrderId, cancellationToken))
                .Where(item => item.FaultTicketId == ticket.Id)
                .Select(item => new PortalShipmentResponse(item.Type, item.Carrier, item.TrackingNumber, item.Status,
                    item.Events.OrderBy(evt => evt.OccurredAt).Select(evt => new PortalShipmentEventResponse(evt.Status,
                        evt.OccurredAt, evt.Location, evt.Description)).ToArray())).ToArray();
            result.Add(new PortalFaultResponse(ticket.Id, ticket.Number, ticket.ProductUnitId, modelName,
                unit?.SerialNumber ?? "-", ticket.Category, ticket.Severity, ticket.Description, ticket.Status,
                ticket.OpenedAt, ticket.History.OrderBy(item => item.OccurredAt).Select(item =>
                    new PortalFaultStatusResponse(item.Previous, item.Current, item.OccurredAt, item.Note)).ToArray(), shipments));
        }
        return result.OrderByDescending(item => item.OpenedAt).ToArray();
    }
}
