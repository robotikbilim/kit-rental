using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Returns;
using KitRental.Core.Domain.Support;

namespace KitRental.Core.Api.Contracts.Requests;

public sealed record CreateProductModelRequest(string Name, string Sku, string? Description = null, string? ImageUrl = null);
public sealed record UpdateProductModelRequest(string Name, string Sku, string? Description = null, string? ImageUrl = null);
public sealed record CreateProductUnitRequest(Guid ProductModelId, string? SerialNumber = null, string? QrCode = null);
public sealed record CreateProductUnitsRequest(Guid ProductModelId, int Quantity);
public sealed record UpdateProductUnitRequest(string SerialNumber, string QrCode);
public sealed record RentPhysicalKitRequest(string CustomerName, string Email, string Phone, string AddressLine,
    string District, string City, string PostalCode, DateOnly StartDate, DateOnly EndDate);
public sealed record BulkRentPhysicalKitsRequest(IReadOnlyCollection<Guid> ProductUnitIds, string CustomerName,
    string Email, string Phone, string AddressLine, string District, string City, string PostalCode,
    DateOnly StartDate, DateOnly EndDate);
public sealed record CreateComponentRequest(string Name, string Sku, string UnitOfMeasure, decimal MinimumStock,
    string? ImageUrl = null, Guid? DefaultStorageLocationId = null, decimal InitialStock = 0);
public sealed record AdjustComponentStockRequest(decimal Change);
public sealed record UpdateComponentRequest(string Name, string Sku, string UnitOfMeasure, decimal MinimumStock,
    string? ImageUrl = null, Guid? DefaultStorageLocationId = null);
public sealed record SupplyNeedLineRequest(Guid ComponentId, decimal Quantity);
public sealed record SupplyNeedRequest(IReadOnlyCollection<SupplyNeedLineRequest> Lines);
public sealed record CompleteSupplyNeedRequest(Guid StorageLocationId, IReadOnlyCollection<SupplyNeedLineRequest> Lines);
public sealed record CreateStorageLocationRequest(string Code, string Warehouse, string Aisle, string Rack, string Shelf,
    bool IsDefaultForNewComponents = false);
public sealed record RecordComponentStockRequest(Guid ComponentId, Guid StorageLocationId, decimal Quantity, string Reference);
public sealed record TransferComponentStockRequest(Guid ComponentId, Guid FromStorageLocationId, Guid ToStorageLocationId, decimal Quantity, string Reference);
public sealed record BillOfMaterialsLineRequest(Guid ComponentId, decimal Quantity);
public sealed record CreateBillOfMaterialsRequest(int Version, IReadOnlyCollection<BillOfMaterialsLineRequest> Lines);
public sealed record CreateKitRequest(string Name, string Sku, string? Description, string? ImageUrl, int BomVersion,
    IReadOnlyCollection<BillOfMaterialsLineRequest> Lines);
public sealed record AddressRequest(string Title, string ContactName, string Phone, string Line1, string District, string City, string PostalCode);
public sealed record CreateCustomerRequest(string Name, string Email, AddressRequest Address,
    IReadOnlyCollection<Guid>? AllowedProductModelIds = null);
public sealed record UpdateCustomerRequest(string Name, string Email, bool IsActive,
    IReadOnlyCollection<Guid>? AllowedProductModelIds = null);
public sealed record OrderLineRequest(Guid ProductModelId, int Quantity);
public sealed record CreateOrderRequest(Guid CustomerId, Guid AddressId, DateOnly StartDate, DateOnly EndDate, IReadOnlyCollection<OrderLineRequest> Lines);
public sealed record OrderTransitionRequest(RentalOrderStatus Target);
public sealed record CreateOrderKitsRequest(IReadOnlyCollection<OrderLineRequest> Lines, bool UseAvailableKits = false,
    Guid? RentalCohortId = null);
public sealed record RentalCohortRequest(string Name, DateOnly StartDate, DateOnly EndDate);
public sealed record RentalCohortStudentRequest(string FullName, string GuardianPhone, string AddressLine,
    int CityId, int DistrictId, string City, string District, Guid ProductModelId);
public sealed record RentalCohortImportRowRequest(string FullName, string GuardianPhone, string AddressLine,
    int CityId, int DistrictId, string City, string District, string ProductModel);
public sealed record RentalCohortStudentImportRequest(IReadOnlyCollection<RentalCohortImportRowRequest> Rows);
public sealed record CreatePurchaseOrderRequest(Guid CustomerId, Guid AddressId,
    IReadOnlyCollection<OrderLineRequest> Lines);
public sealed record CreateRentalAssignmentRequest(Guid OrderLineId, Guid CustomerId, Guid ProductUnitId, DateOnly StartDate, DateOnly EndDate);
public sealed record CreateShipmentRequest(Guid OrderId, Guid? FaultTicketId, ShipmentType Type, string Carrier, string TrackingNumber);
public sealed record ShipmentEventRequest(ShipmentStatus Status, DateTimeOffset OccurredAt, string Location, string Description);
public sealed record OpenFaultRequest(Guid CustomerId, Guid OrderId, Guid AssignmentId, Guid ProductUnitId, string Category, FaultSeverity Severity, string Description);
public sealed record FaultStatusRequest(FaultStatus Status, string Note);
public sealed record FaultGuideEntryRequest(string Title, string Problem, string Solution, int DisplayOrder,
    bool IsActive = true, Guid? ProductModelId = null);
public sealed record InspectionItemRequest(string Name, bool IsPresent, bool IsDamaged, string Note);
public sealed record CompleteInspectionRequest(Guid OrderId, Guid ProductUnitId, IReadOnlyCollection<InspectionItemRequest> Items, decimal DamageCharge, ProductUnitStatus Outcome);
public sealed record PortalFaultRequest(Guid AssignmentId, string ReporterName, string ReporterPhone,
    string ReporterAddress, string District, string City, string Description);
public sealed record PublicFaultRequest(Guid? FaultId, string Token, string ReporterName, string ReporterPhone,
    string ReporterAddress, string District, string City, string Description,
    double? Latitude = null, double? Longitude = null);
public sealed record PublicKitReturnRequest(string Token, string RequesterName,
    string RequesterPhone, string District, string City, string ReturnAddress,
    double? Latitude, double? Longitude, KitReturnReason? ReturnReason = null,
    KitReturnDeliveryMethod DeliveryMethod = KitReturnDeliveryMethod.PickupFromAddress);
public sealed record PortalReturnRequest(IReadOnlyCollection<Guid> AssignmentIds);
public sealed record PortalStudentReturnRequest(string RequesterName, string RequesterPhone, string District,
    string City, string ReturnAddress, KitReturnReason? ReturnReason = null);
public sealed record PortalReturnShipmentRequest(string Carrier, string TrackingNumber);
public sealed record PublicKitDeliveryRequest(string Token, string RecipientName,
    string RecipientPhone, string AddressLine, string District, string City, double? Latitude = null, double? Longitude = null);
