namespace KitRental.Core.Domain.Inventory;

public sealed record InventoryEvent(
    Guid Id,
    Guid ProductUnitId,
    ProductUnitStatus? PreviousStatus,
    ProductUnitStatus NewStatus,
    DateTimeOffset OccurredAt,
    Guid ActorId,
    string Reason);
