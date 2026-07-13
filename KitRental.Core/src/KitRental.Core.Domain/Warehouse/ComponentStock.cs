using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Warehouse;

public sealed class ComponentStock
{
    private ComponentStock()
    {
    }

    private ComponentStock(Guid id, Guid componentId, Guid storageLocationId)
    {
        Id = id;
        ComponentId = componentId;
        StorageLocationId = storageLocationId;
    }

    public Guid Id { get; private set; }
    public Guid ComponentId { get; private set; }
    public Guid StorageLocationId { get; private set; }
    public decimal Quantity { get; private set; }

    public static ComponentStock Create(Guid id, Guid componentId, Guid storageLocationId)
    {
        if (id == Guid.Empty || componentId == Guid.Empty || storageLocationId == Guid.Empty)
            throw new DomainException("component_stock.ids_required", "Stok bakiyesi için komponent ve lokasyon zorunludur.");
        return new ComponentStock(id, componentId, storageLocationId);
    }

    public void Apply(decimal quantityDelta)
    {
        if (quantityDelta == 0)
            throw new DomainException("component_stock.delta_required", "Stok hareket miktarı sıfır olamaz.");
        if (Quantity + quantityDelta < 0)
            throw new DomainException("component_stock.insufficient", "Lokasyondaki komponent stoğu yetersiz.");
        Quantity += quantityDelta;
    }
}
