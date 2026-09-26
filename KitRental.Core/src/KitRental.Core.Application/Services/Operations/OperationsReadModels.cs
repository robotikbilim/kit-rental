namespace KitRental.Core.Application.Operations;

public sealed record OperationsCustomerOption(Guid Id, string Name);

public sealed record OperationsOrderQuery(Guid? CustomerId = null, string? Query = null,
    int? Type = null, int? Status = null, string? Focus = null, DateOnly? EndsFrom = null,
    DateOnly? EndsTo = null, string? Sort = null, int Page = 1, int PageSize = 20);

public sealed record OperationsOrderSummary(Guid Id, string OrderNumber, Guid CustomerId,
    string CustomerName, int Type, int Status, string? RentalPeriodName, DateOnly? StartDate,
    DateOnly? EndDate, DateTimeOffset CreatedAt, string KitDescription, int RequestedKitCount,
    int AssignedKitCount, int StudentCount, int MissingAddressCount, int AwaitingShipmentCount,
    int ShipmentReadyCount, int ShipmentInTransitCount, int ShipmentDeliveredCount,
    int ShipmentFailedCount, int ActiveKitCount, int OpenFaultCount, int PendingReturnCount,
    int InTransitReturnCount, int CompletedReturnCount, int MissingReturnFormCount,
    bool IsOverdue, bool IsEndingSoon);

public sealed record OperationsOrderPage(int Page, int PageSize, int TotalCount, int TotalPages,
    IReadOnlyCollection<OperationsOrderSummary> Items,
    IReadOnlyCollection<OperationsCustomerOption> Customers);

public sealed record OperationsDashboardResponse(int TotalOrders, int TotalStudents,
    int StudentsAwaitingAddress, int StudentsAwaitingShipment, int ShipmentsInTransit,
    int ShipmentsDelivered, int ReturnPendingKitCount, int ReturnInTransitKitCount,
    int ReturnCompletedKitCount, int FaultsAwaitingReview, int FaultsInRepair,
    int FaultsAwaitingShipment, int FaultsCompleted,
    int PendingApprovalOrders, int ActiveKitCount, int ShipmentsReady, int ShipmentsFailed,
    int OpenFaultCount, int OverdueOrders, int EndingSoonOrders, int MissingReturnFormCount,
    DateTimeOffset GeneratedAt, Guid? CustomerId,
    IReadOnlyCollection<OperationsCustomerOption> Customers,
    IReadOnlyCollection<OperationsOrderSummary> PriorityOrders);
