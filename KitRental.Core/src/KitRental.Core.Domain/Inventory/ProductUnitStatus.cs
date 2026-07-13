namespace KitRental.Core.Domain.Inventory;

public enum ProductUnitStatus
{
    Available = 1,
    Reserved = 2,
    Preparing = 3,
    OutboundInTransit = 4,
    WithCustomer = 5,
    ReturnInTransit = 6,
    UnderInspection = 7,
    InMaintenance = 8,
    Quarantined = 9,
    Lost = 10,
    Retired = 11
}
