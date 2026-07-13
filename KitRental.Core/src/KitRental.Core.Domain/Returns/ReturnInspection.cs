using KitRental.Core.Domain.Inventory;
using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Returns;

public sealed record InspectionItem(Guid Id, string Name, bool IsPresent, bool IsDamaged, string Note);

public sealed class ReturnInspection
{
    private readonly List<InspectionItem> _items = [];

    private ReturnInspection() { }
    private ReturnInspection(Guid id, Guid orderId, Guid productUnitId, IReadOnlyCollection<InspectionItem> items, decimal damageCharge, ProductUnitStatus outcome, DateTimeOffset completedAt, Guid completedBy)
    {
        Id = id; OrderId = orderId; ProductUnitId = productUnitId; _items.AddRange(items); DamageCharge = damageCharge;
        Outcome = outcome; CompletedAt = completedAt; CompletedBy = completedBy;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductUnitId { get; private set; }
    public IReadOnlyCollection<InspectionItem> Items => _items.AsReadOnly();
    public decimal DamageCharge { get; private set; }
    public ProductUnitStatus Outcome { get; private set; }
    public DateTimeOffset CompletedAt { get; private set; }
    public Guid CompletedBy { get; private set; }

    public static ReturnInspection Complete(Guid id, Guid orderId, Guid productUnitId, IReadOnlyCollection<InspectionItem> items, decimal damageCharge, ProductUnitStatus outcome, DateTimeOffset completedAt, Guid completedBy)
    {
        if (id == Guid.Empty || orderId == Guid.Empty || productUnitId == Guid.Empty || completedBy == Guid.Empty || items.Count == 0 || damageCharge < 0)
            throw new DomainException("inspection.required_fields", "İade kontrolü için ürün, kontrol listesi ve geçerli mali sonuç zorunludur.");
        if (outcome is not (ProductUnitStatus.Available or ProductUnitStatus.InMaintenance or ProductUnitStatus.Quarantined or ProductUnitStatus.Retired))
            throw new DomainException("inspection.invalid_outcome", "Geçersiz ürün kontrol sonucu.");
        return new ReturnInspection(id, orderId, productUnitId, items, damageCharge, outcome, completedAt, completedBy);
    }
}
