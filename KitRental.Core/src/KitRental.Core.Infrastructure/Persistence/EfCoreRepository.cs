using System.Data;
using KitRental.Core.Application.Abstractions;
using KitRental.Core.Domain.Auditing;
using KitRental.Core.Domain.Customers;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Rentals;
using KitRental.Core.Domain.Returns;
using KitRental.Core.Domain.Support;
using KitRental.Core.Domain.Manufacturing;
using KitRental.Core.Domain.Warehouse;
using KitRental.Core.Domain.Procurement;
using Microsoft.EntityFrameworkCore;

namespace KitRental.Core.Infrastructure.Persistence;

public sealed class EfCoreRepository(KitRentalDbContext dbContext) : ICoreRepository
{
    public async Task AddProductModelAsync(ProductModel model, CancellationToken cancellationToken)
    {
        if (await dbContext.ProductModels.AnyAsync(existing => existing.Sku == model.Sku, cancellationToken))
            throw new InvalidOperationException($"'{model.Sku}' SKU değerine sahip bir ürün modeli zaten var.");
        await dbContext.ProductModels.AddAsync(model, cancellationToken);
    }

    public Task<ProductModel?> GetProductModelAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.ProductModels.SingleOrDefaultAsync(model => model.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<ProductModel>> GetProductModelsAsync(CancellationToken cancellationToken) =>
        await dbContext.ProductModels.OrderBy(model => model.Name).ToArrayAsync(cancellationToken);

    public async Task RemoveProductModelAsync(ProductModel model, CancellationToken cancellationToken)
    {
        var boms = await dbContext.BillsOfMaterials.Where(item => item.ProductModelId == model.Id)
            .ToArrayAsync(cancellationToken);
        dbContext.BillsOfMaterials.RemoveRange(boms);
        dbContext.ProductModels.Remove(model);
    }

    public async Task AddProductUnitAsync(ProductUnit unit, CancellationToken cancellationToken)
    {
        if (await dbContext.ProductUnits.AnyAsync(
                existing => existing.SerialNumber == unit.SerialNumber || existing.QrCode == unit.QrCode,
                cancellationToken))
            throw new InvalidOperationException("Seri numarası veya QR kod başka bir fiziksel ürün biriminde kullanılıyor.");
        await dbContext.ProductUnits.AddAsync(unit, cancellationToken);
    }

    public Task<ProductUnit?> GetProductUnitAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.ProductUnits.Include(unit => unit.History).SingleOrDefaultAsync(unit => unit.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<ProductUnit>> GetProductUnitsAsync(CancellationToken cancellationToken) =>
        await dbContext.ProductUnits.Include(unit => unit.History).OrderBy(unit => unit.SerialNumber).ToArrayAsync(cancellationToken);

    public Task RemoveProductUnitAsync(ProductUnit unit, CancellationToken cancellationToken)
    {
        dbContext.ProductUnits.Remove(unit);
        return Task.CompletedTask;
    }

    public async Task RemoveProductUnitWithStockRestorationAsync(ProductUnit unit,
        IReadOnlyCollection<StockMovement> movements, AuditEntry auditEntry,
        CancellationToken cancellationToken)
    {
        foreach (var movement in movements)
        {
            var stock = await dbContext.ComponentStocks.SingleOrDefaultAsync(item =>
                item.ComponentId == movement.ComponentId &&
                item.StorageLocationId == movement.StorageLocationId, cancellationToken);
            if (stock is null)
            {
                stock = ComponentStock.Create(Guid.NewGuid(), movement.ComponentId, movement.StorageLocationId);
                await dbContext.ComponentStocks.AddAsync(stock, cancellationToken);
            }
            stock.Apply(movement.SignedQuantity);
        }
        dbContext.ProductUnits.Remove(unit);
        await dbContext.StockMovements.AddRangeAsync(movements, cancellationToken);
        await dbContext.AuditEntries.AddAsync(auditEntry, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddProductUnitsWithStockConsumptionAsync(
        IReadOnlyCollection<ProductUnit> units,
        IReadOnlyCollection<StockMovement> movements,
        IReadOnlyCollection<AuditEntry> auditEntries,
        CancellationToken cancellationToken)
    {
        var serialNumbers = units.Select(item => item.SerialNumber).ToArray();
        var qrCodes = units.Select(item => item.QrCode).ToArray();
        if (serialNumbers.Distinct().Count() != units.Count || qrCodes.Distinct().Count() != units.Count ||
            await dbContext.ProductUnits.AnyAsync(item =>
                serialNumbers.Contains(item.SerialNumber) || qrCodes.Contains(item.QrCode), cancellationToken))
            throw new InvalidOperationException("Seri numarası veya QR kod başka bir fiziksel ürün biriminde kullanılıyor.");

        foreach (var movement in movements)
        {
            var stock = await dbContext.ComponentStocks.SingleAsync(item =>
                item.ComponentId == movement.ComponentId &&
                item.StorageLocationId == movement.StorageLocationId, cancellationToken);
            stock.Apply(movement.SignedQuantity);
        }

        await dbContext.ProductUnits.AddRangeAsync(units, cancellationToken);
        await dbContext.StockMovements.AddRangeAsync(movements, cancellationToken);
        await dbContext.AuditEntries.AddRangeAsync(auditEntries, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken)
    {
        if (await dbContext.Customers.AnyAsync(existing => existing.Email == customer.Email, cancellationToken))
            throw new InvalidOperationException("Müşteri e-posta adresi benzersiz olmalıdır.");
        await dbContext.Customers.AddAsync(customer, cancellationToken);
    }

    public Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Customers.Include(customer => customer.Addresses).SingleOrDefaultAsync(customer => customer.Id == id, cancellationToken);

    public Task<Customer?> FindCustomerByEmailAsync(string email, CancellationToken cancellationToken) =>
        dbContext.Customers.Include(customer => customer.Addresses)
            .SingleOrDefaultAsync(customer => customer.Email == email.Trim().ToLower(), cancellationToken);

    public async Task<IReadOnlyCollection<Customer>> GetCustomersAsync(CancellationToken cancellationToken) =>
        await dbContext.Customers.Include(customer => customer.Addresses).OrderBy(customer => customer.Name).ToArrayAsync(cancellationToken);

    public Task AddOrderAsync(RentalOrder order, CancellationToken cancellationToken) =>
        dbContext.RentalOrders.AddAsync(order, cancellationToken).AsTask();

    public Task<RentalOrder?> GetOrderAsync(Guid id, CancellationToken cancellationToken) =>
        OrdersQuery().SingleOrDefaultAsync(order => order.Id == id, cancellationToken);

    public Task<RentalOrder?> FindOrderByLineIdAsync(Guid lineId, CancellationToken cancellationToken) =>
        OrdersQuery().SingleOrDefaultAsync(order => order.Lines.Any(line => line.Id == lineId), cancellationToken);

    public async Task<IReadOnlyCollection<RentalOrder>> GetOrdersAsync(Guid? customerId, CancellationToken cancellationToken)
    {
        var query = OrdersQuery();
        if (customerId.HasValue)
            query = query.Where(order => order.CustomerId == customerId.Value);
        return await query.OrderByDescending(order => order.CreatedAt).ToArrayAsync(cancellationToken);
    }

    public Task<RentalAssignment?> GetRentalAssignmentAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.RentalAssignments.SingleOrDefaultAsync(assignment => assignment.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<RentalAssignment>> GetAssignmentsForOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var lineIds = await dbContext.RentalOrders
            .Where(order => order.Id == orderId)
            .SelectMany(order => order.Lines.Select(line => line.Id))
            .ToArrayAsync(cancellationToken);
        return await dbContext.RentalAssignments
            .Where(assignment => lineIds.Contains(assignment.OrderLineId))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<RentalAssignment>> GetAssignmentsForProductUnitAsync(Guid productUnitId, CancellationToken cancellationToken) =>
        await dbContext.RentalAssignments.Where(item => item.ProductUnitId == productUnitId)
            .OrderByDescending(item => item.CreatedAt).ToArrayAsync(cancellationToken);

    public async Task AddShipmentAsync(Shipment shipment, CancellationToken cancellationToken)
    {
        if (await dbContext.Shipments.AnyAsync(
                existing => existing.TrackingNumber == shipment.TrackingNumber,
                cancellationToken))
            throw new InvalidOperationException("Kargo takip numarası benzersiz olmalıdır.");
        await dbContext.Shipments.AddAsync(shipment, cancellationToken);
    }

    public Task<Shipment?> GetShipmentAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Shipments.Include(shipment => shipment.Events).SingleOrDefaultAsync(shipment => shipment.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<Shipment>> GetShipmentsAsync(Guid orderId, CancellationToken cancellationToken) =>
        await dbContext.Shipments.Include(shipment => shipment.Events)
            .Where(shipment => shipment.OrderId == orderId)
            .ToArrayAsync(cancellationToken);

    public Task AddFaultTicketAsync(FaultTicket ticket, CancellationToken cancellationToken) =>
        dbContext.FaultTickets.AddAsync(ticket, cancellationToken).AsTask();

    public Task<FaultTicket?> GetFaultTicketAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.FaultTickets.Include(ticket => ticket.History).SingleOrDefaultAsync(ticket => ticket.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<FaultTicket>> GetFaultTicketsAsync(Guid? customerId, CancellationToken cancellationToken)
    {
        var query = dbContext.FaultTickets.Include(ticket => ticket.History).AsQueryable();
        if (customerId.HasValue)
            query = query.Where(ticket => ticket.CustomerId == customerId.Value);
        return await query.OrderByDescending(ticket => ticket.OpenedAt).ToArrayAsync(cancellationToken);
    }

    public Task AddInspectionAsync(ReturnInspection inspection, CancellationToken cancellationToken) =>
        dbContext.ReturnInspections.AddAsync(inspection, cancellationToken).AsTask();

    public Task AddKitReturnRequestAsync(KitReturnRequest request, CancellationToken cancellationToken) =>
        dbContext.KitReturnRequests.AddAsync(request, cancellationToken).AsTask();

    public Task<KitReturnRequest?> GetKitReturnRequestAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.KitReturnRequests.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<KitReturnRequest>> GetKitReturnRequestsAsync(Guid? customerId, CancellationToken cancellationToken)
    {
        var query = dbContext.KitReturnRequests.Include(x => x.Items).AsQueryable();
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        return await query.OrderByDescending(x => x.CreatedAt).ToArrayAsync(cancellationToken);
    }

    public Task AddAuditEntryAsync(AuditEntry entry, CancellationToken cancellationToken) =>
        dbContext.AuditEntries.AddAsync(entry, cancellationToken).AsTask();

    public async Task<(IReadOnlyCollection<AuditEntry> Items, int TotalCount)> GetAuditEntriesAsync(
        string? action, Guid? actorId, DateTimeOffset? occurredFrom, DateTimeOffset? occurredTo,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.AuditEntries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(entry => entry.Action == action);
        if (actorId.HasValue)
            query = query.Where(entry => entry.ActorId == actorId.Value);
        if (occurredFrom.HasValue)
            query = query.Where(entry => entry.OccurredAt >= occurredFrom.Value);
        if (occurredTo.HasValue)
            query = query.Where(entry => entry.OccurredAt < occurredTo.Value);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task AddComponentAsync(Component component, CancellationToken cancellationToken)
    {
        if (await dbContext.Components.AnyAsync(existing => existing.Sku == component.Sku, cancellationToken))
            throw new InvalidOperationException($"'{component.Sku}' SKU değerine sahip komponent zaten var.");
        await dbContext.Components.AddAsync(component, cancellationToken);
    }

    public Task<Component?> GetComponentAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Components.SingleOrDefaultAsync(component => component.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<Component>> GetComponentsAsync(CancellationToken cancellationToken) =>
        await dbContext.Components.OrderBy(component => component.Name).ToArrayAsync(cancellationToken);

    public Task RemoveComponentAsync(Component component, CancellationToken cancellationToken)
    {
        dbContext.Components.Remove(component);
        return Task.CompletedTask;
    }

    public async Task AddStorageLocationAsync(StorageLocation location, CancellationToken cancellationToken)
    {
        if (await dbContext.StorageLocations.AnyAsync(existing => existing.Code == location.Code, cancellationToken))
            throw new InvalidOperationException($"'{location.Code}' kodlu lokasyon zaten var.");
        await dbContext.StorageLocations.AddAsync(location, cancellationToken);
    }

    public Task<StorageLocation?> GetStorageLocationAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.StorageLocations.SingleOrDefaultAsync(location => location.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<StorageLocation>> GetStorageLocationsAsync(CancellationToken cancellationToken) =>
        await dbContext.StorageLocations.OrderBy(location => location.Code).ToArrayAsync(cancellationToken);

    public Task RemoveStorageLocationAsync(StorageLocation location, CancellationToken cancellationToken)
    {
        dbContext.StorageLocations.Remove(location);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyCollection<ComponentStock>> GetComponentStocksAsync(
        Guid? componentId,
        Guid? locationId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ComponentStocks.AsQueryable();
        if (componentId.HasValue) query = query.Where(item => item.ComponentId == componentId.Value);
        if (locationId.HasValue) query = query.Where(item => item.StorageLocationId == locationId.Value);
        return await query.Where(item => item.Quantity > 0).ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<StockMovement>> GetStockMovementsAsync(Guid? componentId, CancellationToken cancellationToken)
    {
        var query = dbContext.StockMovements.AsQueryable();
        if (componentId.HasValue) query = query.Where(item => item.ComponentId == componentId.Value);
        return await query.OrderByDescending(item => item.OccurredAt).ToArrayAsync(cancellationToken);
    }

    public async Task ApplyStockMovementsAsync(IReadOnlyCollection<StockMovement> movements, CancellationToken cancellationToken)
    {
        foreach (var movement in movements)
        {
            var stock = await dbContext.ComponentStocks.SingleOrDefaultAsync(
                item => item.ComponentId == movement.ComponentId && item.StorageLocationId == movement.StorageLocationId,
                cancellationToken);
            if (stock is null)
            {
                stock = ComponentStock.Create(Guid.NewGuid(), movement.ComponentId, movement.StorageLocationId);
                await dbContext.ComponentStocks.AddAsync(stock, cancellationToken);
            }
            stock.Apply(movement.SignedQuantity);
            await dbContext.StockMovements.AddAsync(movement, cancellationToken);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddBillOfMaterialsAsync(BillOfMaterials bom, CancellationToken cancellationToken)
    {
        if (await dbContext.BillsOfMaterials.AnyAsync(
                existing => existing.ProductModelId == bom.ProductModelId && existing.Version == bom.Version,
                cancellationToken))
            throw new InvalidOperationException("Bu ürün modeli ve sürüm için reçete zaten var.");
        var activeBoms = await dbContext.BillsOfMaterials
            .Where(existing => existing.ProductModelId == bom.ProductModelId && existing.IsActive)
            .ToArrayAsync(cancellationToken);
        foreach (var active in activeBoms)
            active.Deactivate();
        await dbContext.BillsOfMaterials.AddAsync(bom, cancellationToken);
    }

    public Task<BillOfMaterials?> GetActiveBillOfMaterialsAsync(Guid productModelId, CancellationToken cancellationToken) =>
        dbContext.BillsOfMaterials.Include(item => item.Lines)
            .Where(item => item.ProductModelId == productModelId && item.IsActive)
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<BillOfMaterials>> GetActiveBillOfMaterialsAsync(CancellationToken cancellationToken)
    {
        var active = await dbContext.BillsOfMaterials.Include(item => item.Lines)
            .Where(item => item.IsActive)
            .OrderByDescending(item => item.Version)
            .ToArrayAsync(cancellationToken);
        return active.GroupBy(item => item.ProductModelId).Select(group => group.First()).ToArray();
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await dbContext.SaveChangesAsync(cancellationToken);

    public Task AddSupplyNeedListAsync(SupplyNeedList list, CancellationToken cancellationToken) =>
        dbContext.SupplyNeedLists.AddAsync(list, cancellationToken).AsTask();

    public Task<SupplyNeedList?> GetSupplyNeedListAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.SupplyNeedLists.Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<SupplyNeedList>> GetSupplyNeedListsAsync(CancellationToken cancellationToken) =>
        await dbContext.SupplyNeedLists.Include(item => item.Lines)
            .OrderByDescending(item => item.CreatedAt).ToArrayAsync(cancellationToken);

    public Task RemoveSupplyNeedListAsync(SupplyNeedList list, CancellationToken cancellationToken)
    {
        dbContext.SupplyNeedLists.Remove(list);
        return Task.CompletedTask;
    }

    public Task<bool> TryCreateReservationAsync(
        ProductUnit unit,
        RentalAssignment assignment,
        Guid actorId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken) =>
        TryCreateReservationsAsync([unit], [assignment], actorId, occurredAt, cancellationToken);

    public async Task<bool> TryCreateReservationsAsync(
        IReadOnlyCollection<ProductUnit> units,
        IReadOnlyCollection<RentalAssignment> assignments,
        Guid actorId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var unitIds = units.Select(item => item.Id).ToArray();
        var assignmentUnitIds = assignments.Select(item => item.ProductUnitId).ToArray();
        if (units.Count == 0 || units.Count != assignments.Count ||
            unitIds.Distinct().Count() != units.Count || assignmentUnitIds.Distinct().Count() != assignments.Count ||
            !unitIds.ToHashSet().SetEquals(assignmentUnitIds))
            throw new ArgumentException("Fiziksel kit ve kiralama atamaları birebir eşleşmelidir.");

        var newUnitIds = dbContext.ChangeTracker.Entries<ProductUnit>()
            .Where(entry => entry.State == EntityState.Added && unitIds.Contains(entry.Entity.Id))
            .Select(entry => entry.Entity.Id)
            .ToHashSet();
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var databaseUnits = await dbContext.ProductUnits
            .Where(item => unitIds.Contains(item.Id))
            .Select(item => new { item.Id, item.Status })
            .ToArrayAsync(cancellationToken);
        if (databaseUnits.Length + newUnitIds.Count != units.Count ||
            databaseUnits.Any(item => item.Status != ProductUnitStatus.Available))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var candidates = await dbContext.RentalAssignments
            .Where(existing =>
                unitIds.Contains(existing.ProductUnitId) &&
                (existing.Status == RentalAssignmentStatus.Reserved || existing.Status == RentalAssignmentStatus.Active))
            .ToArrayAsync(cancellationToken);
        var requestedPeriods = assignments.ToDictionary(item => item.ProductUnitId, item => item.Period);
        if (candidates.Any(existing => existing.Period.Overlaps(requestedPeriods[existing.ProductUnitId])))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        foreach (var unit in units)
            unit.Reserve(actorId, occurredAt);
        await dbContext.RentalAssignments.AddRangeAsync(assignments, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
        });
    }

    public async Task<bool> TryReserveUnitsAsync(IReadOnlyCollection<ProductUnit> units, Guid actorId,
        DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var unitIds = units.Select(item => item.Id).ToArray();
        if (units.Count == 0 || unitIds.Distinct().Count() != units.Count)
            throw new ArgumentException("Rezerve edilecek fiziksel kitler benzersiz olmalıdır.");

        var newUnitIds = dbContext.ChangeTracker.Entries<ProductUnit>()
            .Where(entry => entry.State == EntityState.Added && unitIds.Contains(entry.Entity.Id))
            .Select(entry => entry.Entity.Id)
            .ToHashSet();
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var databaseUnits = await dbContext.ProductUnits
                .Where(item => unitIds.Contains(item.Id))
                .Select(item => new { item.Id, item.Status })
                .ToArrayAsync(cancellationToken);
            if (databaseUnits.Length + newUnitIds.Count != units.Count ||
                databaseUnits.Any(item => item.Status != ProductUnitStatus.Available))
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            foreach (var unit in units)
                unit.Reserve(actorId, occurredAt);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    private IQueryable<RentalOrder> OrdersQuery() =>
        dbContext.RentalOrders
            .Include(order => order.DeliveryAddress)
            .Include(order => order.Lines)
            .Include(order => order.ProductUnits)
            .Include(order => order.History);
}
