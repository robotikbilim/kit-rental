using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Inventory;

public sealed class ProductUnit
{
    private readonly List<InventoryEvent> _history = [];

    private ProductUnit()
    {
    }

    private ProductUnit(Guid id, Guid productModelId, string serialNumber, string qrCode)
    {
        Id = id;
        ProductModelId = productModelId;
        SerialNumber = serialNumber;
        QrCode = qrCode;
        Status = ProductUnitStatus.Available;
    }

    public Guid Id { get; private set; }
    public Guid ProductModelId { get; private set; }
    public string SerialNumber { get; private set; } = string.Empty;
    public string QrCode { get; private set; } = string.Empty;
    public ProductUnitStatus Status { get; private set; }
    public IReadOnlyCollection<InventoryEvent> History => _history.AsReadOnly();

    public static ProductUnit Create(
        Guid id,
        Guid productModelId,
        string serialNumber,
        string qrCode,
        Guid actorId,
        DateTimeOffset occurredAt)
    {
        if (id == Guid.Empty || productModelId == Guid.Empty || actorId == Guid.Empty)
        {
            throw new DomainException(
                "product_unit.id_required",
                "Ürün birimi, ürün modeli ve işlemi yapan aktör kimliği zorunludur.");
        }

        if (string.IsNullOrWhiteSpace(serialNumber) || string.IsNullOrWhiteSpace(qrCode))
        {
            throw new DomainException("product_unit.identifiers_required", "Seri numarası ve QR kod zorunludur.");
        }

        var unit = new ProductUnit(
            id,
            productModelId,
            serialNumber.Trim().ToUpperInvariant(),
            qrCode.Trim().ToUpperInvariant());

        unit._history.Add(new InventoryEvent(
            Guid.NewGuid(), id, null, ProductUnitStatus.Available, occurredAt, actorId, "Fiziksel ürün birimi oluşturuldu."));

        return unit;
    }

    public void UpdateIdentifiers(string serialNumber, string qrCode)
    {
        if (string.IsNullOrWhiteSpace(serialNumber) || string.IsNullOrWhiteSpace(qrCode))
            throw new DomainException("product_unit.identifiers_required", "Seri numarası ve QR kod zorunludur.");
        SerialNumber = serialNumber.Trim().ToUpperInvariant();
        QrCode = qrCode.Trim().ToUpperInvariant();
    }

    public void Reserve(Guid actorId, DateTimeOffset occurredAt) =>
        TransitionTo(ProductUnitStatus.Reserved, actorId, occurredAt, "Kiralama için rezerve edildi.", ProductUnitStatus.Available);

    public void ReleaseReservation(Guid actorId, DateTimeOffset occurredAt) =>
        TransitionTo(ProductUnitStatus.Available, actorId, occurredAt, "Rezervasyon serbest bırakıldı.", ProductUnitStatus.Reserved);

    public void StartPreparation(Guid actorId, DateTimeOffset occurredAt) =>
        TransitionTo(ProductUnitStatus.Preparing, actorId, occurredAt, "Kit kiraya verildi; gönderim bekliyor.", ProductUnitStatus.Reserved);

    public void Dispatch(Guid actorId, DateTimeOffset occurredAt) =>
        TransitionTo(ProductUnitStatus.OutboundInTransit, actorId, occurredAt, "Çıkış kargosuna verildi.", ProductUnitStatus.Preparing);

    public void ConfirmDelivery(Guid actorId, DateTimeOffset occurredAt) =>
        TransitionTo(ProductUnitStatus.WithCustomer, actorId, occurredAt, "Teslimat doğrulandı.", ProductUnitStatus.OutboundInTransit);

    public void CompleteSale(Guid actorId, DateTimeOffset occurredAt) =>
        TransitionTo(ProductUnitStatus.Sold, actorId, occurredAt, "Satış teslimatı tamamlandı; kit kiralama filosundan çıkarıldı.",
            ProductUnitStatus.OutboundInTransit);

    public void StartReturn(Guid actorId, DateTimeOffset occurredAt) =>
        TransitionTo(ProductUnitStatus.ReturnInTransit, actorId, occurredAt, "İade kargosuna verildi.", ProductUnitStatus.WithCustomer);

    public void ReceiveForInspection(Guid actorId, DateTimeOffset occurredAt) =>
        TransitionTo(ProductUnitStatus.UnderInspection, actorId, occurredAt, "Depoya kabul edildi; kontrol bekliyor.", ProductUnitStatus.ReturnInTransit);

    public void CompleteInspection(ProductUnitStatus outcome, Guid actorId, DateTimeOffset occurredAt, string reason)
    {
        if (outcome is not (ProductUnitStatus.Available or ProductUnitStatus.InMaintenance or ProductUnitStatus.Quarantined or ProductUnitStatus.Retired))
        {
            throw new DomainException("product_unit.invalid_inspection_outcome", "Geçersiz iade kontrol sonucu.");
        }

        TransitionTo(outcome, actorId, occurredAt, reason, ProductUnitStatus.UnderInspection);
    }

    public void ReceiveReturnToAvailable(Guid actorId, DateTimeOffset occurredAt)
    {
        ReceiveForInspection(actorId, occurredAt);
        CompleteInspection(ProductUnitStatus.Available, actorId, occurredAt, "Müşteri iadesi teslim alındı; kit yeniden kiralanabilir.");
    }

    private void TransitionTo(
        ProductUnitStatus next,
        Guid actorId,
        DateTimeOffset occurredAt,
        string reason,
        params ProductUnitStatus[] allowedCurrentStatuses)
    {
        if (!allowedCurrentStatuses.Contains(Status))
        {
            throw new DomainException(
                "product_unit.invalid_status_transition",
                $"Ürün birimi {Status} durumundan {next} durumuna geçirilemez.");
        }

        if (actorId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("inventory_event.audit_data_required", "Durum değişikliği için aktör ve gerekçe zorunludur.");
        }

        var previous = Status;
        Status = next;
        _history.Add(new InventoryEvent(Guid.NewGuid(), Id, previous, next, occurredAt, actorId, reason.Trim()));
    }
}
