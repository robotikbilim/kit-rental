using KitRental.Core.Application.Abstractions;
using KitRental.Core.Application.Common;
using KitRental.Core.Domain.Auditing;
using KitRental.Core.Domain.Customers;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Rentals;
using KitRental.Core.Domain.Support;
using KitRental.SharedKernel;

namespace KitRental.Core.Application.PhysicalKits;

public sealed class PhysicalKitService(ICoreRepository repository, TimeProvider timeProvider)
{
    public async Task<PhysicalKitDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var items = await GetListAsync(cancellationToken);
        return new PhysicalKitDashboardResponse(
            items.Count,
            items.Count(item => item.Status == ProductUnitStatus.Available),
            items.Count(item => item.Status == ProductUnitStatus.WithCustomer),
            items.Count(item => item.Status is ProductUnitStatus.Reserved or ProductUnitStatus.Preparing),
            items.Count(item => item.Status is ProductUnitStatus.OutboundInTransit or ProductUnitStatus.ReturnInTransit),
            items.Count(item => item.Status is ProductUnitStatus.UnderInspection or ProductUnitStatus.InMaintenance or ProductUnitStatus.Quarantined),
            items.Where(item => item.Status == ProductUnitStatus.Available).ToArray(),
            items.Where(item => item.Status == ProductUnitStatus.WithCustomer).ToArray(), items);
    }

    public async Task<IReadOnlyCollection<PhysicalKitListItemResponse>> GetListAsync(CancellationToken cancellationToken)
    {
        var units = await repository.GetProductUnitsAsync(cancellationToken);
        var models = (await repository.GetProductModelsAsync(cancellationToken)).ToDictionary(item => item.Id);
        var result = new List<PhysicalKitListItemResponse>();
        foreach (var unit in units)
        {
            if (!models.TryGetValue(unit.ProductModelId, out var model)) continue;
            result.Add(await MapListItemAsync(unit, model, cancellationToken));
        }
        return result.OrderBy(item => item.KitName).ThenBy(item => item.SerialNumber).ToArray();
    }

    public async Task<IReadOnlyCollection<PhysicalKitModelSummaryResponse>> GetModelSummariesAsync(
        CancellationToken cancellationToken)
    {
        var units = await repository.GetProductUnitsAsync(cancellationToken);
        var faultyUnitIds = await GetFaultyUnitIdsAsync(cancellationToken);
        return (await repository.GetProductModelsAsync(cancellationToken)).Select(model =>
        {
            var modelUnits = units.Where(unit => unit.ProductModelId == model.Id).ToArray();
            return new PhysicalKitModelSummaryResponse(model.Id, model.Name, model.Sku, model.ImageUrl,
                modelUnits.Length,
                modelUnits.Count(unit => unit.Status == ProductUnitStatus.Available && !IsFaulty(unit, faultyUnitIds)),
                modelUnits.Count(unit => IsFaulty(unit, faultyUnitIds)));
        }).OrderBy(item => item.KitName).ToArray();
    }

    public async Task<PhysicalKitUnitPageResponse> GetModelUnitsAsync(Guid productModelId, string? filter,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var model = await repository.GetProductModelAsync(productModelId, cancellationToken)
            ?? throw new ResourceNotFoundException("Eğitim kiti bulunamadı.");
        var normalizedFilter = NormalizeFilter(filter);
        var faultyUnitIds = await GetFaultyUnitIdsAsync(cancellationToken);
        var units = (await repository.GetProductUnitsAsync(cancellationToken))
            .Where(unit => unit.ProductModelId == productModelId)
            .Where(unit => MatchesFilter(unit, normalizedFilter, faultyUnitIds))
            .OrderBy(unit => unit.SerialNumber)
            .ToArray();
        var validPageSize = Math.Clamp(pageSize, 10, 100);
        var totalPages = Math.Max(1, (int)Math.Ceiling(units.Length / (double)validPageSize));
        var validPage = Math.Clamp(page, 1, totalPages);
        var items = new List<PhysicalKitListItemResponse>();
        foreach (var unit in units.Skip((validPage - 1) * validPageSize).Take(validPageSize))
            items.Add(await MapListItemAsync(unit, model, cancellationToken));
        return new PhysicalKitUnitPageResponse(model.Id, model.Name, model.Sku, model.ImageUrl, normalizedFilter,
            validPage, validPageSize, units.Length, totalPages, items);
    }

    public async Task<IReadOnlyCollection<PhysicalKitListItemResponse>> GetModelUnitsForLabelsAsync(
        Guid productModelId, string? filter, CancellationToken cancellationToken)
    {
        var model = await repository.GetProductModelAsync(productModelId, cancellationToken)
            ?? throw new ResourceNotFoundException("Eğitim kiti bulunamadı.");
        var normalizedFilter = NormalizeFilter(filter);
        var faultyUnitIds = await GetFaultyUnitIdsAsync(cancellationToken);
        var units = (await repository.GetProductUnitsAsync(cancellationToken))
            .Where(unit => unit.ProductModelId == productModelId && MatchesFilter(unit, normalizedFilter, faultyUnitIds))
            .OrderBy(unit => unit.SerialNumber).ToArray();
        var items = new List<PhysicalKitListItemResponse>(units.Length);
        foreach (var unit in units)
            items.Add(await MapListItemAsync(unit, model, cancellationToken));
        return items;
    }

    public async Task<PhysicalKitDetailResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var unit = await repository.GetProductUnitAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("Fiziksel kit bulunamadı.");
        var model = await repository.GetProductModelAsync(unit.ProductModelId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kit modeli bulunamadı.");
        var assignments = await repository.GetAssignmentsForProductUnitAsync(id, cancellationToken);
        var rentals = new List<PhysicalKitRentalHistoryResponse>();
        foreach (var assignment in assignments)
        {
            var order = await repository.FindOrderByLineIdAsync(assignment.OrderLineId, cancellationToken);
            var customer = await repository.GetCustomerAsync(assignment.CustomerId, cancellationToken);
            if (order is null || customer is null) continue;
            var address = order.DeliveryAddress;
            rentals.Add(new PhysicalKitRentalHistoryResponse(assignment.Id, order.OrderNumber, order.Status,
                assignment.Status, customer.Name, customer.Email,
                $"{address.Line1}, {address.District} / {address.City}", assignment.Period.StartDate,
                assignment.Period.EndDate, assignment.CreatedAt));
        }
        var faults = (await repository.GetFaultTicketsAsync(null, cancellationToken))
            .Where(item => item.ProductUnitId == id)
            .Select(item => new PhysicalKitFaultHistoryResponse(item.Number, item.Category, item.Severity, item.Status,
                item.Description, item.OpenedAt, item.History.OrderByDescending(history => history.OccurredAt)
                    .Select(history => $"{history.OccurredAt:dd.MM.yyyy}: {history.Note}").ToArray())).ToArray();
        var status = unit.History.OrderByDescending(item => item.OccurredAt)
            .Select(item => new PhysicalKitStatusEventResponse(item.PreviousStatus, item.NewStatus, item.OccurredAt, item.Reason)).ToArray();
        return new PhysicalKitDetailResponse(await MapListItemAsync(unit, model, cancellationToken), rentals, faults, status);
    }

    public async Task<PhysicalKitDetailResponse> LookupAsync(string identifier, CancellationToken cancellationToken)
    {
        var value = identifier.Trim();
        var unit = (await repository.GetProductUnitsAsync(cancellationToken)).FirstOrDefault(item =>
            string.Equals(item.SerialNumber, value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.QrCode, value, StringComparison.OrdinalIgnoreCase));
        if (unit is null)
            throw new ResourceNotFoundException("Bu seri numarası veya QR kodla eşleşen fiziksel kit bulunamadı.");
        return await GetDetailAsync(unit.Id, cancellationToken);
    }

    public async Task<RentPhysicalKitResponse> RentAsync(RentPhysicalKitCommand command, CancellationToken cancellationToken)
    {
        var unit = await repository.GetProductUnitAsync(command.ProductUnitId, cancellationToken)
            ?? throw new ResourceNotFoundException("Fiziksel kit bulunamadı.");
        if (unit.Status != ProductUnitStatus.Available)
            throw new ConflictException("physical_kit.not_available", "Yalnızca kiralanabilir durumdaki bir kit kiralanabilir.");
        _ = await repository.GetProductModelAsync(unit.ProductModelId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kit modeli bulunamadı.");
        if ((await GetFaultyUnitIdsAsync(cancellationToken)).Contains(unit.Id))
            throw new ConflictException("physical_kit.has_open_fault", "Açık arıza kaydı bulunan bir kit kiralanamaz.");

        var customer = await repository.FindCustomerByEmailAsync(command.Email, cancellationToken);
        if (customer is null)
        {
            customer = Customer.Create(Guid.NewGuid(), command.CustomerName, command.Email);
            await repository.AddCustomerAsync(customer, cancellationToken);
        }
        var address = customer.AddAddress("Kiralama adresi", command.CustomerName, command.Phone,
            command.AddressLine, command.District, command.City, command.PostalCode);
        var now = timeProvider.GetUtcNow();
        var period = new RentalPeriod(command.StartDate, command.EndDate);
        var order = RentalOrder.Create(Guid.NewGuid(), $"KR-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..20],
            customer.Id, period, customer.SnapshotAddress(address.Id), now);
        var line = order.AddLine(unit.ProductModelId, 1);
        order.Submit(command.ActorId, now);
        order.Approve(command.ActorId, now);
        await repository.AddOrderAsync(order, cancellationToken);

        var assignment = RentalAssignment.Create(Guid.NewGuid(), line.Id, customer.Id, unit.Id, period, now, command.ActorId);
        if (!await repository.TryCreateReservationAsync(unit, assignment, command.ActorId, now, cancellationToken))
            throw new ConflictException("rental_assignment.overlap", "Kit bu tarih aralığında başka bir kiralamaya atanmış.");

        order.StartPreparation(command.ActorId, now);
        unit.StartPreparation(command.ActorId, now);
        order.MarkReadyToShip(command.ActorId, now);
        order.Dispatch(command.ActorId, now);
        unit.Dispatch(command.ActorId, now);
        order.ConfirmDelivery(command.ActorId, now);
        unit.ConfirmDelivery(command.ActorId, now);
        order.ActivateRental(command.ActorId, now);
        assignment.Activate();
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), command.ActorId, nameof(ProductUnit), unit.Id,
            "Rented", ProductUnitStatus.Available.ToString(), unit.Status.ToString(), now), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return new RentPhysicalKitResponse(unit.Id, customer.Id, order.Id, assignment.Id, order.OrderNumber, unit.SerialNumber, unit.Status);
    }

    public async Task<BulkRentPhysicalKitsResponse> RentManyAsync(BulkRentPhysicalKitsCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ProductUnitIds.Count == 0)
            throw new DomainException("physical_kit.selection_required", "Kiralamak için en az bir fiziksel kit seçilmelidir.");
        if (command.ProductUnitIds.Count > 100)
            throw new DomainException("physical_kit.selection_limit", "Tek işlemde en fazla 100 fiziksel kit kiralanabilir.");

        var unitIds = command.ProductUnitIds.Distinct().ToArray();
        if (unitIds.Length != command.ProductUnitIds.Count)
            throw new DomainException("physical_kit.duplicate_selection", "Aynı fiziksel kit birden fazla kez seçilemez.");

        var units = new List<ProductUnit>(unitIds.Length);
        foreach (var unitId in unitIds)
        {
            var unit = await repository.GetProductUnitAsync(unitId, cancellationToken)
                ?? throw new ResourceNotFoundException("Seçilen fiziksel kitlerden biri bulunamadı.");
            if (unit.Status != ProductUnitStatus.Available)
                throw new ConflictException("physical_kit.not_available",
                    $"{unit.SerialNumber} seri numaralı kit artık kiralanabilir durumda değil.");
            _ = await repository.GetProductModelAsync(unit.ProductModelId, cancellationToken)
                ?? throw new ResourceNotFoundException("Seçilen kitlerden birinin ürün modeli bulunamadı.");
            units.Add(unit);
        }

        var faultyUnitIds = await GetFaultyUnitIdsAsync(cancellationToken);
        var faultyUnit = units.FirstOrDefault(unit => faultyUnitIds.Contains(unit.Id));
        if (faultyUnit is not null)
            throw new ConflictException("physical_kit.has_open_fault",
                $"{faultyUnit.SerialNumber} seri numaralı kitin açık arıza kaydı bulunuyor.");

        var customer = await repository.FindCustomerByEmailAsync(command.Email, cancellationToken);
        if (customer is null)
        {
            customer = Customer.Create(Guid.NewGuid(), command.CustomerName, command.Email);
            await repository.AddCustomerAsync(customer, cancellationToken);
        }

        var address = customer.AddAddress("Kiralama adresi", command.CustomerName, command.Phone,
            command.AddressLine, command.District, command.City, command.PostalCode);
        var now = timeProvider.GetUtcNow();
        var period = new RentalPeriod(command.StartDate, command.EndDate);
        var order = RentalOrder.Create(Guid.NewGuid(), $"KR-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..20],
            customer.Id, period, customer.SnapshotAddress(address.Id), now);
        var linesByModel = units.GroupBy(unit => unit.ProductModelId)
            .ToDictionary(group => group.Key, group => order.AddLine(group.Key, group.Count()));
        order.Submit(command.ActorId, now);
        order.Approve(command.ActorId, now);
        await repository.AddOrderAsync(order, cancellationToken);

        var assignments = units.Select(unit => RentalAssignment.Create(Guid.NewGuid(),
            linesByModel[unit.ProductModelId].Id, customer.Id, unit.Id, period, now, command.ActorId)).ToArray();
        if (!await repository.TryCreateReservationsAsync(units, assignments, command.ActorId, now, cancellationToken))
            throw new ConflictException("rental_assignment.overlap",
                "Seçilen kitlerden biri başka bir işlem tarafından kiralandı. Listeyi yenileyip tekrar deneyin.");

        order.StartPreparation(command.ActorId, now);
        foreach (var unit in units)
            unit.StartPreparation(command.ActorId, now);
        order.MarkReadyToShip(command.ActorId, now);
        order.Dispatch(command.ActorId, now);
        foreach (var unit in units)
            unit.Dispatch(command.ActorId, now);
        order.ConfirmDelivery(command.ActorId, now);
        foreach (var unit in units)
            unit.ConfirmDelivery(command.ActorId, now);
        order.ActivateRental(command.ActorId, now);
        foreach (var assignment in assignments)
            assignment.Activate();

        foreach (var unit in units)
            await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), command.ActorId, nameof(ProductUnit),
                unit.Id, "BulkRented", ProductUnitStatus.Available.ToString(), unit.Status.ToString(), now), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        var assignmentByUnit = assignments.ToDictionary(item => item.ProductUnitId);
        var items = units.OrderBy(item => item.SerialNumber).Select(unit => new BulkRentPhysicalKitItemResponse(
            unit.Id, assignmentByUnit[unit.Id].Id, unit.SerialNumber, unit.Status)).ToArray();
        return new BulkRentPhysicalKitsResponse(customer.Id, order.Id, order.OrderNumber, items.Length, items);
    }

    private async Task<PhysicalKitListItemResponse> MapListItemAsync(ProductUnit unit, ProductModel model,
        CancellationToken cancellationToken)
    {
        PhysicalKitCurrentRentalResponse? current = null;
        var assignment = (await repository.GetAssignmentsForProductUnitAsync(unit.Id, cancellationToken))
            .FirstOrDefault(item => item.Status == RentalAssignmentStatus.Active);
        if (assignment is not null)
        {
            var customer = await repository.GetCustomerAsync(assignment.CustomerId, cancellationToken);
            var order = await repository.FindOrderByLineIdAsync(assignment.OrderLineId, cancellationToken);
            if (customer is not null && order is not null)
                current = new PhysicalKitCurrentRentalResponse(customer.Name, order.DeliveryAddress.City,
                    assignment.Period.StartDate, assignment.Period.EndDate);
        }
        return new PhysicalKitListItemResponse(unit.Id, model.Id, model.Name, model.Sku, model.ImageUrl,
            unit.SerialNumber, unit.QrCode, unit.Status, current);
    }

    private async Task<HashSet<Guid>> GetFaultyUnitIdsAsync(CancellationToken cancellationToken) =>
        (await repository.GetFaultTicketsAsync(null, cancellationToken))
            .Where(ticket => ticket.Status is not (FaultStatus.Resolved or FaultStatus.Closed))
            .Select(ticket => ticket.ProductUnitId).ToHashSet();

    private static bool IsFaulty(ProductUnit unit, IReadOnlySet<Guid> faultyUnitIds) =>
        unit.Status is ProductUnitStatus.InMaintenance or ProductUnitStatus.Quarantined || faultyUnitIds.Contains(unit.Id);

    private static string NormalizeFilter(string? filter) => filter?.Trim().ToLowerInvariant() switch
    {
        "available" => "available",
        "faulty" => "faulty",
        _ => "all"
    };

    private static bool MatchesFilter(ProductUnit unit, string filter, IReadOnlySet<Guid> faultyUnitIds) => filter switch
    {
        "available" => unit.Status == ProductUnitStatus.Available && !IsFaulty(unit, faultyUnitIds),
        "faulty" => IsFaulty(unit, faultyUnitIds),
        _ => true
    };
}
