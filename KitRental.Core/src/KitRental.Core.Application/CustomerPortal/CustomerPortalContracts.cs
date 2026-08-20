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
public sealed record PortalRentalCohortStudentResponse(Guid Id, string FullName, string GuardianPhone,
    string AddressLine, int? CityId, int? DistrictId, string City, string District, Guid ProductModelId, string ProductModelName, string ProductModelSku, Guid? OrderId,
    Guid? AssignmentId, Guid? ProductUnitId, string? SerialNumber, string? QrCode, bool IsDeleted,
    bool HasActiveReturn, bool HasDeliveryForm = false, string? DeliveredTo = null,
    string? DeliveryPhone = null, string? DeliveryAddress = null, string? DeliveryDistrict = null,
    string? DeliveryCity = null, DateTimeOffset? DeliveredAt = null);
public sealed record PortalUnassignedCohortKitResponse(Guid ProductUnitId, Guid AssignmentId, Guid OrderId,
    Guid ProductModelId, string ProductModelName, string ProductModelSku, string SerialNumber, string QrCode);
public sealed record PortalRentalCohortResponse(Guid Id, Guid CustomerId, string Name, DateOnly StartDate,
    DateOnly EndDate, DateTimeOffset CreatedAt, Guid? OrderId, int StudentCount, int AssignedKitCount,
    IReadOnlyCollection<PortalRentalCohortStudentResponse> Students,
    IReadOnlyCollection<PortalUnassignedCohortKitResponse> UnassignedKits,
    string? OrderNumber = null, RentalOrderStatus? OrderStatus = null, bool IsApproved = false);
public sealed record PortalKitResponse(Guid ProductUnitId, Guid AssignmentId, Guid OrderId, string OrderNumber,
    string KitName, string KitSku, string? ImageUrl, string SerialNumber, string QrCode, ProductUnitStatus UnitStatus,
    RentalAssignmentStatus AssignmentStatus, DateOnly StartDate, DateOnly EndDate, int OpenFaultCount,
    bool HasDeliveryForm, string? AssignedStudentName = null, string? AssignedStudentGuardianPhone = null,
    string? AssignedStudentAddressLine = null, string? AssignedStudentPeriodName = null, bool IsReturned = false);
public sealed record PortalOrderLineResponse(Guid ProductModelId, string ProductName, string ProductSku, int Quantity);
public sealed record PortalOrderResponse(Guid Id, string OrderNumber, Guid CustomerId, string CustomerName,
    OrderType Type, RentalOrderStatus Status, DateOnly? StartDate, DateOnly? EndDate, DateTimeOffset CreatedAt,
    IReadOnlyCollection<PortalOrderLineResponse> Lines, int AssignedKitCount = 0);
public sealed record PortalFaultStatusResponse(FaultStatus Previous, FaultStatus Current, DateTimeOffset OccurredAt, string Note);
public sealed record PortalShipmentEventResponse(ShipmentStatus Status, DateTimeOffset OccurredAt, string Location, string Description);
public sealed record PortalShipmentResponse(ShipmentType Type, string Carrier, string TrackingNumber, ShipmentStatus Status,
    IReadOnlyCollection<PortalShipmentEventResponse> Events);
public sealed record PortalFaultResponse(Guid Id, string Number, Guid ProductUnitId, string KitName, string SerialNumber,
    string Category, FaultSeverity Severity, string Description, FaultStatus Status, DateTimeOffset OpenedAt,
    IReadOnlyCollection<PortalFaultStatusResponse> History, IReadOnlyCollection<PortalShipmentResponse> Shipments,
    string ReporterName = "", string ReporterPhone = "", string ReporterAddress = "",
    FaultApprovalStatus ApprovalStatus = FaultApprovalStatus.NotRequired);
public sealed record CustomerPortalResponse(string CustomerName, string CustomerEmail, int TotalRentedKitCount,
    int UndeliveredKitCount, int ActiveKitCount, int UnassignedKitCount, int PendingRequestCount, int OpenFaultCount,
    int CompletedFaultCount, int ExpiredRentalKitCount, int ReturnProcessStartedKitCount, int ReturnedKitCount,
    IReadOnlyCollection<PortalKitResponse> Kits,
    IReadOnlyCollection<PortalOrderResponse> Orders, IReadOnlyCollection<PortalFaultResponse> Faults,
    IReadOnlyCollection<PortalAddressResponse> Addresses, IReadOnlyCollection<PortalProductModelResponse> ProductModels,
    IReadOnlyCollection<PortalKitReturnResponse> Returns,
    IReadOnlyCollection<PortalKitLocationResponse> KitLocations,
    IReadOnlyCollection<PortalRentalCohortResponse> RentalCohorts);
public sealed record PortalKitLocationResponse(Guid ProductUnitId, Guid ProductModelId, string KitName,
    string KitSku, string SerialNumber, string RecipientName, string AddressLine, string District, string City,
    int Status, double? Latitude = null, double? Longitude = null, string LocationCategory = "active");
public sealed record PortalKitReturnItemResponse(Guid AssignmentId, Guid ProductUnitId, Guid OrderId,
    string KitName, string SerialNumber);
public sealed record PortalKitReturnResponse(Guid Id, Guid CustomerId, string CustomerName, KitReturnStatus Status,
    string? Carrier, string? TrackingNumber, DateTimeOffset CreatedAt, DateTimeOffset? ShippedAt,
    string? RequesterName, string? RequesterPhone, string? ReturnAddress,
    double? Latitude, double? Longitude,
    IReadOnlyCollection<PortalKitReturnItemResponse> Items);

public sealed record OpenPortalFaultCommand(Guid CustomerId, Guid AssignmentId, string Category,
    FaultSeverity Severity, string Description, Guid ActorId);
public sealed record ConfirmPortalOrderDeliveryCommand(Guid CustomerId, Guid OrderId, Guid ActorId);
public sealed record CreatePortalRentalCohortOrderCommand(Guid CustomerId, Guid CohortId, Guid ActorId,
    string ActorDisplayName);
public sealed record CreatePublicKitReturnCommand(string QrCode, string RequesterName,
    string RequesterPhone, string District, string City, string ReturnAddress,
    double? Latitude, double? Longitude);
public sealed record CreatePortalReturnCommand(Guid CustomerId, IReadOnlyCollection<Guid> AssignmentIds,
    Guid ActorId, string ActorDisplayName);
public sealed record ShipPortalReturnCommand(Guid CustomerId, Guid ReturnId, string Carrier,
    string TrackingNumber, Guid ActorId, string ActorDisplayName);
public sealed record SaveRentalCohortCommand(Guid? Id, Guid CustomerId, string Name, DateOnly StartDate,
    DateOnly EndDate, Guid ActorId, string ActorDisplayName);
public sealed record DeleteRentalCohortCommand(Guid CustomerId, Guid CohortId, Guid ActorId);
public sealed record SaveRentalCohortStudentCommand(Guid? Id, Guid CustomerId, Guid CohortId, string FullName,
    string GuardianPhone, string AddressLine, int CityId, int DistrictId, string City, string District, Guid ProductModelId, Guid ActorId, string ActorDisplayName);
public sealed record ImportRentalCohortStudentCommand(string FullName, string GuardianPhone, string AddressLine,
    int CityId, int DistrictId, string City, string District, string ProductModel);
public sealed record CreatePortalStudentReturnCommand(Guid CustomerId, Guid CohortId, Guid StudentId, Guid ActorId,
    string ActorDisplayName);





