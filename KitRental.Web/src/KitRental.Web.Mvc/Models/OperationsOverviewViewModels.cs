using System.ComponentModel.DataAnnotations;

namespace KitRental.Web.Mvc.Models;

public sealed record OperationsCustomerOption(Guid Id, string Name);
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
public sealed record OperationsOrdersScreen(OperationsOrderPage Result, OperationsOrderFilter Filter);

public sealed class OperationsOrderFilter
{
    public Guid? CustomerId { get; set; }
    public string? Query { get; set; }
    public int? Type { get; set; }
    public int? Status { get; set; }
    public string? Focus { get; set; }
    [DataType(DataType.Date)] public DateOnly? EndsFrom { get; set; }
    [DataType(DataType.Date)] public DateOnly? EndsTo { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class OperationsReturnFilter
{
    public Guid? CustomerId { get; set; }
    public Guid? OrderId { get; set; }
    public string? State { get; set; }
}

public sealed record OperationsReturnsScreen(IReadOnlyCollection<ReturnTableItemViewModel> Items,
    OperationsReturnFilter Filter, string? OrderNumber);

public sealed record OperationsDashboardViewModel(int TotalOrders, int TotalStudents,
    int StudentsAwaitingAddress, int StudentsAwaitingShipment, int ShipmentsInTransit,
    int ShipmentsDelivered, int ReturnPendingKitCount, int ReturnInTransitKitCount,
    int ReturnCompletedKitCount, int FaultsAwaitingReview, int FaultsInRepair,
    int FaultsAwaitingShipment, int FaultsCompleted,
    int PendingApprovalOrders, int ActiveKitCount, int ShipmentsReady, int ShipmentsFailed,
    int OpenFaultCount, int OverdueOrders, int EndingSoonOrders, int MissingReturnFormCount,
    DateTimeOffset GeneratedAt, Guid? CustomerId,
    IReadOnlyCollection<OperationsCustomerOption> Customers,
    IReadOnlyCollection<OperationsOrderSummary> PriorityOrders);

public sealed record OperationsMetricViewModel(string Label, int Count, string Description,
    string Action, string? FilterName = null, string? FilterValue = null,
    Guid? CustomerId = null, string Tone = "neutral", string Icon = "arrow-up-right");
