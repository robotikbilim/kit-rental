using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Procurement;

public enum SupplyNeedStatus
{
    Pending = 1,
    Supplied = 2
}

public sealed class SupplyNeedList
{
    private readonly List<SupplyNeedLine> _lines = [];

    private SupplyNeedList() { }

    private SupplyNeedList(Guid id, DateTimeOffset createdAt)
    {
        Id = id;
        CreatedAt = createdAt;
        Status = SupplyNeedStatus.Pending;
    }

    public Guid Id { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public SupplyNeedStatus Status { get; private set; }
    public IReadOnlyCollection<SupplyNeedLine> Lines => _lines.AsReadOnly();

    public static SupplyNeedList Create(Guid id, DateTimeOffset createdAt,
        IEnumerable<(Guid ComponentId, decimal Quantity)> lines)
    {
        if (id == Guid.Empty)
            throw new DomainException("supply_need.id_required", "İhtiyaç listesi kimliği zorunludur.");
        var list = new SupplyNeedList(id, createdAt);
        list.ReplaceLines(lines, createdAt);
        return list;
    }

    public void Update(IEnumerable<(Guid ComponentId, decimal Quantity)> lines, DateTimeOffset updatedAt) =>
        ReplaceLines(lines, updatedAt);

    public void Complete(IEnumerable<(Guid ComponentId, decimal SuppliedQuantity)> suppliedLines,
        DateTimeOffset updatedAt)
    {
        if (Status != SupplyNeedStatus.Pending)
            throw new DomainException("supply_need.already_supplied", "Bu ihtiyaç listesi daha önce tedarik edildi.");
        var supplied = suppliedLines.ToArray();
        if (supplied.Length != _lines.Count || supplied.Select(line => line.ComponentId).Distinct().Count() != supplied.Length ||
            !_lines.Select(line => line.ComponentId).ToHashSet().SetEquals(supplied.Select(line => line.ComponentId)))
            throw new DomainException("supply_need.confirmation_incomplete", "Tüm komponentler tek tek teyit edilmelidir.");
        if (supplied.Any(line => line.SuppliedQuantity <= 0))
            throw new DomainException("supply_need.supplied_quantity_invalid", "Tedarik edilen miktar sıfırdan büyük olmalıdır.");
        var quantities = supplied.ToDictionary(line => line.ComponentId, line => line.SuppliedQuantity);
        foreach (var line in _lines)
            line.Confirm(quantities[line.ComponentId]);
        Status = SupplyNeedStatus.Supplied;
        UpdatedAt = updatedAt;
    }

    private void ReplaceLines(IEnumerable<(Guid ComponentId, decimal Quantity)> lines, DateTimeOffset updatedAt)
    {
        var materialized = lines.ToArray();
        if (materialized.Length == 0)
            throw new DomainException("supply_need.lines_required", "İhtiyaç listesine en az bir komponent eklenmelidir.");
        if (materialized.Any(line => line.ComponentId == Guid.Empty || line.Quantity <= 0))
            throw new DomainException("supply_need.line_invalid", "Komponent ve adet bilgileri geçerli olmalıdır.");
        if (materialized.Select(line => line.ComponentId).Distinct().Count() != materialized.Length)
            throw new DomainException("supply_need.component_duplicate", "Aynı komponent listede birden fazla kez kullanılamaz.");
        _lines.Clear();
        _lines.AddRange(materialized.Select(line => new SupplyNeedLine(Guid.NewGuid(), line.ComponentId, line.Quantity)));
        UpdatedAt = updatedAt;
    }
}

public sealed class SupplyNeedLine
{
    private SupplyNeedLine() { }
    public SupplyNeedLine(Guid id, Guid componentId, decimal quantity)
    {
        Id = id;
        ComponentId = componentId;
        Quantity = quantity;
    }
    public Guid Id { get; private set; }
    public Guid ComponentId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal? SuppliedQuantity { get; private set; }
    public void Confirm(decimal quantity) => SuppliedQuantity = quantity;
}
