using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Manufacturing;

public sealed class BillOfMaterials
{
    private readonly List<BillOfMaterialsLine> _lines = [];

    private BillOfMaterials()
    {
    }

    private BillOfMaterials(Guid id, Guid productModelId, int version)
    {
        Id = id;
        ProductModelId = productModelId;
        Version = version;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid ProductModelId { get; private set; }
    public int Version { get; private set; }
    public bool IsActive { get; private set; }
    public IReadOnlyCollection<BillOfMaterialsLine> Lines => _lines.AsReadOnly();

    public static BillOfMaterials Create(
        Guid id,
        Guid productModelId,
        int version,
        IEnumerable<(Guid ComponentId, decimal Quantity)> lines)
    {
        if (id == Guid.Empty || productModelId == Guid.Empty)
            throw new DomainException("bom.ids_required", "Reçete ve ürün modeli kimliği zorunludur.");
        if (version <= 0)
            throw new DomainException("bom.version_invalid", "Reçete sürümü sıfırdan büyük olmalıdır.");

        var materialLines = lines.ToArray();
        if (materialLines.Length == 0)
            throw new DomainException("bom.lines_required", "Reçetede en az bir komponent bulunmalıdır.");
        if (materialLines.Any(line => line.ComponentId == Guid.Empty || line.Quantity <= 0))
            throw new DomainException("bom.line_invalid", "Reçete satırlarında komponent ve pozitif miktar zorunludur.");
        if (materialLines.Select(line => line.ComponentId).Distinct().Count() != materialLines.Length)
            throw new DomainException("bom.component_duplicate", "Aynı komponent reçetede birden fazla kez kullanılamaz.");

        var bom = new BillOfMaterials(id, productModelId, version);
        bom._lines.AddRange(materialLines.Select(line => new BillOfMaterialsLine(Guid.NewGuid(), line.ComponentId, line.Quantity)));
        return bom;
    }

    public void Deactivate() => IsActive = false;
}

public sealed record BillOfMaterialsLine(Guid Id, Guid ComponentId, decimal Quantity);
