using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Returns;
using KitRental.Core.Domain.Support;

namespace KitRental.Core.Application.Operations;

// Shared classifications keep dashboard counts and the linked lists consistent.
public static class OperationsWorkload
{
    public static bool IsOperational(RentalOrderStatus status) =>
        status is not (RentalOrderStatus.Draft or RentalOrderStatus.Cancelled or RentalOrderStatus.Rejected);

    public static string ReturnState(KitReturnRequest? request) =>
        request?.Status == KitReturnStatus.Received ? "completed" :
        request?.Status == KitReturnStatus.InTransit || request?.ExternalShipmentId.HasValue == true
            ? "in-transit" : "pending";

    public static bool IsOpenFault(FaultStatus status) =>
        status is not (FaultStatus.Resolved or FaultStatus.Closed or FaultStatus.Rejected or FaultStatus.RemoteResolved);

    public static bool MatchesFaultStage(FaultStatus status, string? stage) => stage switch
    {
        "open" => IsOpenFault(status),
        "review" => status is FaultStatus.Open or FaultStatus.Investigating or FaultStatus.WaitingForCustomer,
        "repair" => status is FaultStatus.Accepted or FaultStatus.InService or FaultStatus.WorkshopReceived,
        "shipment" => status is FaultStatus.AwaitingReturn or FaultStatus.AwaitingWorkshopShipment or FaultStatus.Repaired,
        "completed" => status is FaultStatus.Resolved or FaultStatus.Closed or FaultStatus.RemoteResolved,
        _ => true
    };

    public static bool MatchesOrderFocus(OperationsOrderSummary item, string? focus) => focus switch
    {
        "approval" => item.Status == (int)RentalOrderStatus.PendingApproval,
        "address" => item.MissingAddressCount > 0,
        "preparation" => (item.Status is 3 or 4) && item.AssignedKitCount < item.RequestedKitCount,
        "shipment" => item.AwaitingShipmentCount > 0,
        "ready" => item.ShipmentReadyCount > 0,
        "in-transit" => item.ShipmentInTransitCount > 0,
        "delivered" => item.ShipmentDeliveredCount > 0,
        "shipment-failed" => item.ShipmentFailedCount > 0,
        "active" => item.ActiveKitCount > 0,
        "faults" => item.OpenFaultCount > 0,
        "returns" => item.PendingReturnCount + item.InTransitReturnCount > 0,
        "overdue" => item.IsOverdue,
        "ending-soon" => item.IsEndingSoon,
        _ => true
    };
}
