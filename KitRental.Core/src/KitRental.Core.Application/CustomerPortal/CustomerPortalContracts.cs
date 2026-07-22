using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Rentals;
using KitRental.Core.Domain.Support;
using KitRental.Core.Domain.Returns;

namespace KitRental.Core.Application.CustomerPortal;

public sealed record PortalAddressResponse(Guid Id, string Title, string ContactName, string Phone, string Line1,
    string District, string City, string PostalCode);
public sealed record PortalProductModelResponse(Guid Id, string Name, string Sku, string? Description, string? ImageUrl);
public sealed record PortalKitResponse(Guid ProductUnitId, Guid AssignmentId, Guid OrderId, string OrderNumber,
    string KitName, string KitSku, string? ImageUrl, string SerialNumber, string QrCode, ProductUnitStatus UnitStatus,
    RentalAssignmentStatus AssignmentStatus, DateOnly StartDate, DateOnly EndDate, int OpenFaultCount);
public sealed record PortalOrderLineResponse(Guid ProductModelId, string ProductName, string ProductSku, int Quantity);
public sealed record PortalOrderResponse(Guid Id, string OrderNumber, Guid CustomerId, string CustomerName,
    RentalOrderStatus Status, DateOnly StartDate, DateOnly EndDate, DateTimeOffset CreatedAt,
    IReadOnlyCollection<PortalOrderLineResponse> Lines, int AssignedKitCount = 0);
public sealed record PortalFaultStatusResponse(FaultStatus Previous, FaultStatus Current, DateTimeOffset OccurredAt, string Note);
public sealed record PortalShipmentEventResponse(ShipmentStatus Status, DateTimeOffset OccurredAt, string Location, string Description);
public sealed record PortalShipmentResponse(ShipmentType Type, string Carrier, string TrackingNumber, ShipmentStatus Status,
    IReadOnlyCollection<PortalShipmentEventResponse> Events);
public sealed record PortalFaultResponse(Guid Id, string Number, Guid ProductUnitId, string KitName, string SerialNumber,
    string Category, FaultSeverity Severity, string Description, FaultStatus Status, DateTimeOffset OpenedAt,
    IReadOnlyCollection<PortalFaultStatusResponse> History, IReadOnlyCollection<PortalShipmentResponse> Shipments);
public sealed record CustomerPortalResponse(string CustomerName, string CustomerEmail, int ActiveKitCount,
    int PendingRequestCount, int OpenFaultCount, IReadOnlyCollection<PortalKitResponse> Kits,
    IReadOnlyCollection<PortalOrderResponse> Orders, IReadOnlyCollection<PortalFaultResponse> Faults,
    IReadOnlyCollection<PortalAddressResponse> Addresses, IReadOnlyCollection<PortalProductModelResponse> ProductModels,
    IReadOnlyCollection<PortalKitReturnResponse> Returns);
public sealed record PortalKitReturnItemResponse(Guid AssignmentId, Guid ProductUnitId, Guid OrderId,
    string KitName, string SerialNumber);
public sealed record PortalKitReturnResponse(Guid Id, Guid CustomerId, string CustomerName, KitReturnStatus Status,
    string? Carrier, string? TrackingNumber, DateTimeOffset CreatedAt, DateTimeOffset? ShippedAt,
    IReadOnlyCollection<PortalKitReturnItemResponse> Items);

public sealed record CreatePortalRentalRequestCommand(Guid CustomerId, Guid AddressId, DateOnly StartDate,
    DateOnly EndDate, IReadOnlyCollection<PortalRentalLineCommand> Lines, Guid ActorId);
public sealed record PortalRentalLineCommand(Guid ProductModelId, int Quantity);
public sealed record OpenPortalFaultCommand(Guid CustomerId, Guid AssignmentId, string Category,
    FaultSeverity Severity, string Description, Guid ActorId);
public sealed record ConfirmPortalOrderDeliveryCommand(Guid CustomerId, Guid OrderId, Guid ActorId);
public sealed record CreatePortalKitReturnCommand(Guid CustomerId, IReadOnlyCollection<Guid> AssignmentIds, Guid ActorId);
public sealed record ShipPortalKitReturnCommand(Guid CustomerId, Guid ReturnId, string Carrier, string TrackingNumber, Guid ActorId);
