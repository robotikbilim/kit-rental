using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Warehouse;

public enum StockMovementType
{
    Receipt = 1,
    Consumption = 2,
    TransferOut = 3,
    TransferIn = 4,
    AdjustmentIncrease = 5,
    AdjustmentDecrease = 6
}

public sealed class StockMovement
{
    private StockMovement()
    {
    }

    private StockMovement(
        Guid id,
        Guid componentId,
        Guid storageLocationId,
        StockMovementType type,
        decimal quantity,
        string reference,
        Guid actorId,
        DateTimeOffset occurredAt,
        Guid? transferId)
    {
        Id = id;
        ComponentId = componentId;
        StorageLocationId = storageLocationId;
        Type = type;
        Quantity = quantity;
        Reference = reference;
        ActorId = actorId;
        OccurredAt = occurredAt;
        TransferId = transferId;
    }

    public Guid Id { get; private set; }
    public Guid ComponentId { get; private set; }
    public Guid StorageLocationId { get; private set; }
    public StockMovementType Type { get; private set; }
    public decimal Quantity { get; private set; }
    public string Reference { get; private set; } = string.Empty;
    public Guid ActorId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public Guid? TransferId { get; private set; }

    public decimal SignedQuantity => Type is StockMovementType.Receipt or StockMovementType.TransferIn or StockMovementType.AdjustmentIncrease
        ? Quantity
        : -Quantity;

    public static StockMovement Create(
        Guid id,
        Guid componentId,
        Guid storageLocationId,
        StockMovementType type,
        decimal quantity,
        string reference,
        Guid actorId,
        DateTimeOffset occurredAt,
        Guid? transferId = null)
    {
        if (id == Guid.Empty || componentId == Guid.Empty || storageLocationId == Guid.Empty || actorId == Guid.Empty)
            throw new DomainException("stock_movement.ids_required", "Stok hareketi için komponent, lokasyon ve aktör zorunludur.");
        if (quantity <= 0)
            throw new DomainException("stock_movement.quantity_invalid", "Stok hareket miktarı sıfırdan büyük olmalıdır.");
        if (string.IsNullOrWhiteSpace(reference))
            throw new DomainException("stock_movement.reference_required", "Stok hareketi açıklaması veya referansı zorunludur.");

        return new StockMovement(id, componentId, storageLocationId, type, quantity, reference.Trim(), actorId, occurredAt, transferId);
    }
}
