using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Rentals;
using KitRental.Core.Domain.Support;

namespace KitRental.Core.Application.PhysicalKits;

public sealed record PhysicalKitCurrentRentalResponse(string CustomerName, string City, DateOnly StartDate, DateOnly EndDate);
public sealed record PhysicalKitListItemResponse(Guid Id, Guid ProductModelId, string KitName, string KitSku,
    string? ImageUrl, string SerialNumber, string QrCode, ProductUnitStatus Status, PhysicalKitCurrentRentalResponse? CurrentRental);
public sealed record PhysicalKitDashboardResponse(int Total, int Available, int Rented, int Reserved, int InTransit,
    int ServiceOrInspection, IReadOnlyCollection<PhysicalKitListItemResponse> AvailableKits,
    IReadOnlyCollection<PhysicalKitListItemResponse> RentedKits, IReadOnlyCollection<PhysicalKitListItemResponse> AllKits);
public sealed record PhysicalKitModelSummaryResponse(Guid ProductModelId, string KitName, string KitSku,
    string? ImageUrl, int Total, int Available, int Faulty);
public sealed record PhysicalKitUnitPageResponse(Guid ProductModelId, string KitName, string KitSku, string? ImageUrl,
    string Filter, int Page, int PageSize, int TotalCount, int TotalPages,
    IReadOnlyCollection<PhysicalKitListItemResponse> Items);
public sealed record PhysicalKitStatusEventResponse(ProductUnitStatus? PreviousStatus, ProductUnitStatus NewStatus,
    DateTimeOffset OccurredAt, string Reason);
public sealed record PhysicalKitRentalHistoryResponse(Guid AssignmentId, string OrderNumber, RentalOrderStatus OrderStatus,
    RentalAssignmentStatus AssignmentStatus, string CustomerName, string CustomerEmail, string Address,
    DateOnly StartDate, DateOnly EndDate, DateTimeOffset CreatedAt);
public sealed record PhysicalKitFaultHistoryResponse(string Number, string Category, FaultSeverity Severity,
    FaultStatus Status, string Description, DateTimeOffset OpenedAt, IReadOnlyCollection<string> StatusNotes);
public sealed record PhysicalKitDetailResponse(PhysicalKitListItemResponse Kit,
    IReadOnlyCollection<PhysicalKitRentalHistoryResponse> RentalHistory,
    IReadOnlyCollection<PhysicalKitFaultHistoryResponse> FaultHistory,
    IReadOnlyCollection<PhysicalKitStatusEventResponse> StatusHistory);
public sealed record RentPhysicalKitCommand(Guid ProductUnitId, string CustomerName, string Email, string Phone,
    string AddressLine, string District, string City, string PostalCode, DateOnly StartDate, DateOnly EndDate, Guid ActorId);
public sealed record RentPhysicalKitResponse(Guid ProductUnitId, Guid CustomerId, Guid OrderId, Guid AssignmentId,
    string OrderNumber, string SerialNumber, ProductUnitStatus Status);
public sealed record BulkRentPhysicalKitsCommand(IReadOnlyCollection<Guid> ProductUnitIds, string CustomerName,
    string Email, string Phone, string AddressLine, string District, string City, string PostalCode,
    DateOnly StartDate, DateOnly EndDate, Guid ActorId);
public sealed record BulkRentPhysicalKitItemResponse(Guid ProductUnitId, Guid AssignmentId, string SerialNumber,
    ProductUnitStatus Status);
public sealed record BulkRentPhysicalKitsResponse(Guid CustomerId, Guid OrderId, string OrderNumber, int KitCount,
    IReadOnlyCollection<BulkRentPhysicalKitItemResponse> Kits);
