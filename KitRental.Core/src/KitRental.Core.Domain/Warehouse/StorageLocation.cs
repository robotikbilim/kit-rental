using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Warehouse;

public sealed class StorageLocation
{
    private StorageLocation()
    {
    }

    private StorageLocation(Guid id, string code, string warehouse, string aisle, string rack, string shelf,
        bool isDefaultForNewComponents)
    {
        Id = id;
        Code = code;
        Warehouse = warehouse;
        Aisle = aisle;
        Rack = rack;
        Shelf = shelf;
        IsActive = true;
        IsDefaultForNewComponents = isDefaultForNewComponents;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Warehouse { get; private set; } = string.Empty;
    public string Aisle { get; private set; } = string.Empty;
    public string Rack { get; private set; } = string.Empty;
    public string Shelf { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public bool IsDefaultForNewComponents { get; private set; }

    public static StorageLocation Create(Guid id, string code, string warehouse, string aisle, string rack, string shelf,
        bool isDefaultForNewComponents = false)
    {
        if (id == Guid.Empty)
            throw new DomainException("storage_location.id_required", "Raf/lokasyon kimliği zorunludur.");
        if (new[] { code, warehouse, aisle, rack, shelf }.Any(string.IsNullOrWhiteSpace))
            throw new DomainException("storage_location.fields_required", "Lokasyon kodu, depo, koridor, raf ve göz bilgileri zorunludur.");

        return new StorageLocation(
            id,
            code.Trim().ToUpperInvariant(),
            warehouse.Trim(),
            aisle.Trim().ToUpperInvariant(),
            rack.Trim().ToUpperInvariant(),
            shelf.Trim().ToUpperInvariant(),
            isDefaultForNewComponents);
    }

    public void Update(string code, string warehouse, string aisle, string rack, string shelf,
        bool isDefaultForNewComponents = false)
    {
        var updated = Create(Id, code, warehouse, aisle, rack, shelf, isDefaultForNewComponents);
        Code = updated.Code;
        Warehouse = updated.Warehouse;
        Aisle = updated.Aisle;
        Rack = updated.Rack;
        Shelf = updated.Shelf;
        IsDefaultForNewComponents = updated.IsDefaultForNewComponents;
    }

    public void SetDefaultForNewComponents(bool value) => IsDefaultForNewComponents = value;
}
