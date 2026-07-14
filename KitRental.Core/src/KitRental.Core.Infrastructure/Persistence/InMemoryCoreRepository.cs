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

namespace KitRental.Core.Infrastructure.Persistence;

public sealed class InMemoryCoreRepository : ICoreRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, ProductModel> _models = [];
    private readonly Dictionary<Guid, ProductUnit> _units = [];
    private readonly List<RentalAssignment> _assignments = [];
    private readonly Dictionary<Guid, Customer> _customers = [];
    private readonly Dictionary<Guid, RentalOrder> _orders = [];
    private readonly Dictionary<Guid, Shipment> _shipments = [];
    private readonly Dictionary<Guid, FaultTicket> _faultTickets = [];
    private readonly Dictionary<Guid, ReturnInspection> _inspections = [];
    private readonly List<AuditEntry> _auditEntries = [];
    private readonly Dictionary<Guid, Component> _components = [];
    private readonly Dictionary<Guid, StorageLocation> _storageLocations = [];
    private readonly Dictionary<(Guid ComponentId, Guid LocationId), ComponentStock> _componentStocks = [];
    private readonly List<StockMovement> _stockMovements = [];
    private readonly Dictionary<Guid, BillOfMaterials> _boms = [];

    public Task AddProductModelAsync(ProductModel model, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_models.Values.Any(existing => existing.Sku == model.Sku))
            {
                throw new InvalidOperationException($"'{model.Sku}' SKU değerine sahip bir ürün modeli zaten var.");
            }

            _models.Add(model.Id, model);
        }

        return Task.CompletedTask;
    }

    public Task<ProductModel?> GetProductModelAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_models.GetValueOrDefault(id));
        }
    }

    public Task<IReadOnlyCollection<ProductModel>> GetProductModelsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult<IReadOnlyCollection<ProductModel>>(_models.Values.OrderBy(item => item.Name).ToArray());
    }

    public Task AddProductUnitAsync(ProductUnit unit, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_units.Values.Any(existing =>
                    existing.SerialNumber == unit.SerialNumber || existing.QrCode == unit.QrCode))
            {
                throw new InvalidOperationException("Seri numarası veya QR kod başka bir fiziksel ürün biriminde kullanılıyor.");
            }

            _units.Add(unit.Id, unit);
        }

        return Task.CompletedTask;
    }

    public Task<ProductUnit?> GetProductUnitAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_units.GetValueOrDefault(id));
        }
    }

    public Task<IReadOnlyCollection<ProductUnit>> GetProductUnitsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyCollection<ProductUnit>>(_units.Values.ToArray());
        }
    }

    public Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_customers.Values.Any(existing => existing.Email == customer.Email))
                throw new InvalidOperationException("Müşteri e-posta adresi benzersiz olmalıdır.");
            _customers.Add(customer.Id, customer);
        }
        return Task.CompletedTask;
    }

    public Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult(_customers.GetValueOrDefault(id));
    }

    public Task<Customer?> FindCustomerByEmailAsync(string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult(_customers.Values.SingleOrDefault(item =>
            item.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase)));
    }

    public Task<IReadOnlyCollection<Customer>> GetCustomersAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult<IReadOnlyCollection<Customer>>(_customers.Values.ToArray());
    }

    public Task AddOrderAsync(RentalOrder order, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) _orders.Add(order.Id, order);
        return Task.CompletedTask;
    }

    public Task<RentalOrder?> GetOrderAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult(_orders.GetValueOrDefault(id));
    }

    public Task<RentalOrder?> FindOrderByLineIdAsync(Guid lineId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult(_orders.Values.SingleOrDefault(order => order.Lines.Any(line => line.Id == lineId)));
    }

    public Task<IReadOnlyCollection<RentalOrder>> GetOrdersAsync(Guid? customerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            IEnumerable<RentalOrder> orders = customerId.HasValue ? _orders.Values.Where(order => order.CustomerId == customerId.Value) : _orders.Values;
            return Task.FromResult<IReadOnlyCollection<RentalOrder>>(orders.ToArray());
        }
    }

    public Task<RentalAssignment?> GetRentalAssignmentAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult(_assignments.SingleOrDefault(assignment => assignment.Id == id));
    }

    public Task<IReadOnlyCollection<RentalAssignment>> GetAssignmentsForOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_orders.TryGetValue(orderId, out var order))
                return Task.FromResult<IReadOnlyCollection<RentalAssignment>>([]);
            var lineIds = order.Lines.Select(line => line.Id).ToHashSet();
            return Task.FromResult<IReadOnlyCollection<RentalAssignment>>(
                _assignments.Where(assignment => lineIds.Contains(assignment.OrderLineId)).ToArray());
        }
    }

    public Task<IReadOnlyCollection<RentalAssignment>> GetAssignmentsForProductUnitAsync(Guid productUnitId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult<IReadOnlyCollection<RentalAssignment>>(
            _assignments.Where(item => item.ProductUnitId == productUnitId)
                .OrderByDescending(item => item.CreatedAt).ToArray());
    }

    public Task AddShipmentAsync(Shipment shipment, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_shipments.Values.Any(existing => existing.TrackingNumber == shipment.TrackingNumber))
                throw new InvalidOperationException("Kargo takip numarası benzersiz olmalıdır.");
            _shipments.Add(shipment.Id, shipment);
        }
        return Task.CompletedTask;
    }

    public Task<Shipment?> GetShipmentAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult(_shipments.GetValueOrDefault(id));
    }

    public Task<IReadOnlyCollection<Shipment>> GetShipmentsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult<IReadOnlyCollection<Shipment>>(_shipments.Values.Where(shipment => shipment.OrderId == orderId).ToArray());
    }

    public Task AddFaultTicketAsync(FaultTicket ticket, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) _faultTickets.Add(ticket.Id, ticket);
        return Task.CompletedTask;
    }

    public Task<FaultTicket?> GetFaultTicketAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult(_faultTickets.GetValueOrDefault(id));
    }

    public Task<IReadOnlyCollection<FaultTicket>> GetFaultTicketsAsync(Guid? customerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            IEnumerable<FaultTicket> tickets = customerId.HasValue ? _faultTickets.Values.Where(ticket => ticket.CustomerId == customerId.Value) : _faultTickets.Values;
            return Task.FromResult<IReadOnlyCollection<FaultTicket>>(tickets.ToArray());
        }
    }

    public Task AddInspectionAsync(ReturnInspection inspection, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) _inspections.Add(inspection.Id, inspection);
        return Task.CompletedTask;
    }

    public Task AddAuditEntryAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _auditEntries.Add(entry);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<AuditEntry>> GetAuditEntriesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyCollection<AuditEntry>>(_auditEntries.OrderByDescending(entry => entry.OccurredAt).ToArray());
        }
    }

    public Task AddComponentAsync(Component component, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_components.Values.Any(existing => existing.Sku == component.Sku))
                throw new InvalidOperationException($"'{component.Sku}' SKU değerine sahip komponent zaten var.");
            _components.Add(component.Id, component);
        }
        return Task.CompletedTask;
    }

    public Task<Component?> GetComponentAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult(_components.GetValueOrDefault(id));
    }

    public Task<IReadOnlyCollection<Component>> GetComponentsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult<IReadOnlyCollection<Component>>(_components.Values.OrderBy(item => item.Name).ToArray());
    }

    public Task AddStorageLocationAsync(StorageLocation location, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_storageLocations.Values.Any(existing => existing.Code == location.Code))
                throw new InvalidOperationException($"'{location.Code}' kodlu lokasyon zaten var.");
            _storageLocations.Add(location.Id, location);
        }
        return Task.CompletedTask;
    }

    public Task<StorageLocation?> GetStorageLocationAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult(_storageLocations.GetValueOrDefault(id));
    }

    public Task<IReadOnlyCollection<StorageLocation>> GetStorageLocationsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult<IReadOnlyCollection<StorageLocation>>(_storageLocations.Values.OrderBy(item => item.Code).ToArray());
    }

    public Task<IReadOnlyCollection<ComponentStock>> GetComponentStocksAsync(
        Guid? componentId,
        Guid? locationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var stocks = _componentStocks.Values.AsEnumerable();
            if (componentId.HasValue) stocks = stocks.Where(item => item.ComponentId == componentId.Value);
            if (locationId.HasValue) stocks = stocks.Where(item => item.StorageLocationId == locationId.Value);
            return Task.FromResult<IReadOnlyCollection<ComponentStock>>(stocks.Where(item => item.Quantity > 0).ToArray());
        }
    }

    public Task<IReadOnlyCollection<StockMovement>> GetStockMovementsAsync(Guid? componentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var movements = componentId.HasValue
                ? _stockMovements.Where(item => item.ComponentId == componentId.Value)
                : _stockMovements;
            return Task.FromResult<IReadOnlyCollection<StockMovement>>(movements.OrderByDescending(item => item.OccurredAt).ToArray());
        }
    }

    public Task ApplyStockMovementsAsync(IReadOnlyCollection<StockMovement> movements, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            foreach (var movement in movements)
            {
                var key = (movement.ComponentId, movement.StorageLocationId);
                if (!_componentStocks.TryGetValue(key, out var stock))
                {
                    stock = ComponentStock.Create(Guid.NewGuid(), movement.ComponentId, movement.StorageLocationId);
                    _componentStocks.Add(key, stock);
                }
                stock.Apply(movement.SignedQuantity);
                _stockMovements.Add(movement);
            }
        }
        return Task.CompletedTask;
    }

    public Task AddBillOfMaterialsAsync(BillOfMaterials bom, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_boms.Values.Any(existing => existing.ProductModelId == bom.ProductModelId && existing.Version == bom.Version))
                throw new InvalidOperationException("Bu ürün modeli ve sürüm için reçete zaten var.");
            foreach (var active in _boms.Values.Where(existing => existing.ProductModelId == bom.ProductModelId && existing.IsActive))
                active.Deactivate();
            _boms.Add(bom.Id, bom);
        }
        return Task.CompletedTask;
    }

    public Task<BillOfMaterials?> GetActiveBillOfMaterialsAsync(Guid productModelId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_boms.Values.Where(item => item.ProductModelId == productModelId && item.IsActive)
                .OrderByDescending(item => item.Version).FirstOrDefault());
        }
    }

    public Task<IReadOnlyCollection<BillOfMaterials>> GetActiveBillOfMaterialsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyCollection<BillOfMaterials>>(_boms.Values.Where(item => item.IsActive)
                .GroupBy(item => item.ProductModelId).Select(group => group.OrderByDescending(item => item.Version).First()).ToArray());
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task<bool> TryCreateReservationAsync(
        ProductUnit unit,
        RentalAssignment assignment,
        Guid actorId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken) =>
        TryCreateReservationsAsync([unit], [assignment], actorId, occurredAt, cancellationToken);

    public Task<bool> TryCreateReservationsAsync(
        IReadOnlyCollection<ProductUnit> units,
        IReadOnlyCollection<RentalAssignment> assignments,
        Guid actorId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var unitIds = units.Select(item => item.Id).ToHashSet();
        var assignmentUnitIds = assignments.Select(item => item.ProductUnitId).ToArray();
        if (units.Count == 0 || units.Count != assignments.Count ||
            unitIds.Count != units.Count || assignmentUnitIds.Distinct().Count() != assignments.Count ||
            !unitIds.SetEquals(assignmentUnitIds))
            throw new ArgumentException("Fiziksel kit ve kiralama atamaları birebir eşleşmelidir.");

        lock (_gate)
        {
            var requestedPeriods = assignments.ToDictionary(item => item.ProductUnitId, item => item.Period);
            var overlaps = units.Any(item => item.Status != ProductUnitStatus.Available) ||
                _assignments.Any(existing => unitIds.Contains(existing.ProductUnitId) && existing.BlocksAvailability &&
                    existing.Period.Overlaps(requestedPeriods[existing.ProductUnitId]));

            if (overlaps)
                return Task.FromResult(false);

            foreach (var unit in units)
                unit.Reserve(actorId, occurredAt);
            _assignments.AddRange(assignments);
            return Task.FromResult(true);
        }
    }
}
