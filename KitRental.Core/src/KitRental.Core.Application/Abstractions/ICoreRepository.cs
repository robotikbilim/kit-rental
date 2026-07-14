using KitRental.Core.Domain.Customers;
using KitRental.Core.Domain.Auditing;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Rentals;
using KitRental.Core.Domain.Returns;
using KitRental.Core.Domain.Support;
using KitRental.Core.Domain.Manufacturing;
using KitRental.Core.Domain.Warehouse;

namespace KitRental.Core.Application.Abstractions;

public interface ICoreRepository
{
    Task AddProductModelAsync(ProductModel model, CancellationToken cancellationToken);
    Task<ProductModel?> GetProductModelAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ProductModel>> GetProductModelsAsync(CancellationToken cancellationToken);
    Task AddProductUnitAsync(ProductUnit unit, CancellationToken cancellationToken);
    Task<ProductUnit?> GetProductUnitAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ProductUnit>> GetProductUnitsAsync(CancellationToken cancellationToken);
    Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken);
    Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken);
    Task<Customer?> FindCustomerByEmailAsync(string email, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Customer>> GetCustomersAsync(CancellationToken cancellationToken);
    Task AddOrderAsync(RentalOrder order, CancellationToken cancellationToken);
    Task<RentalOrder?> GetOrderAsync(Guid id, CancellationToken cancellationToken);
    Task<RentalOrder?> FindOrderByLineIdAsync(Guid lineId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<RentalOrder>> GetOrdersAsync(Guid? customerId, CancellationToken cancellationToken);
    Task<RentalAssignment?> GetRentalAssignmentAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<RentalAssignment>> GetAssignmentsForOrderAsync(Guid orderId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<RentalAssignment>> GetAssignmentsForProductUnitAsync(Guid productUnitId, CancellationToken cancellationToken);
    Task AddShipmentAsync(Shipment shipment, CancellationToken cancellationToken);
    Task<Shipment?> GetShipmentAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Shipment>> GetShipmentsAsync(Guid orderId, CancellationToken cancellationToken);
    Task AddFaultTicketAsync(FaultTicket ticket, CancellationToken cancellationToken);
    Task<FaultTicket?> GetFaultTicketAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<FaultTicket>> GetFaultTicketsAsync(Guid? customerId, CancellationToken cancellationToken);
    Task AddInspectionAsync(ReturnInspection inspection, CancellationToken cancellationToken);
    Task AddAuditEntryAsync(AuditEntry entry, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AuditEntry>> GetAuditEntriesAsync(CancellationToken cancellationToken);
    Task AddComponentAsync(Component component, CancellationToken cancellationToken);
    Task<Component?> GetComponentAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Component>> GetComponentsAsync(CancellationToken cancellationToken);
    Task AddStorageLocationAsync(StorageLocation location, CancellationToken cancellationToken);
    Task<StorageLocation?> GetStorageLocationAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<StorageLocation>> GetStorageLocationsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ComponentStock>> GetComponentStocksAsync(Guid? componentId, Guid? locationId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<StockMovement>> GetStockMovementsAsync(Guid? componentId, CancellationToken cancellationToken);
    Task ApplyStockMovementsAsync(IReadOnlyCollection<StockMovement> movements, CancellationToken cancellationToken);
    Task AddBillOfMaterialsAsync(BillOfMaterials bom, CancellationToken cancellationToken);
    Task<BillOfMaterials?> GetActiveBillOfMaterialsAsync(Guid productModelId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<BillOfMaterials>> GetActiveBillOfMaterialsAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<bool> TryCreateReservationAsync(
        ProductUnit unit,
        RentalAssignment assignment,
        Guid actorId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken);
    Task<bool> TryCreateReservationsAsync(
        IReadOnlyCollection<ProductUnit> units,
        IReadOnlyCollection<RentalAssignment> assignments,
        Guid actorId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken);
}
