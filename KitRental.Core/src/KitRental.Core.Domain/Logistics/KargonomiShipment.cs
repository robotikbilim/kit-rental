using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Logistics;

public enum KargonomiShipmentState
{
    Pending = 1,
    Draft = 2,
    Ready = 3,
    InTransit = 4,
    Delivered = 5,
    Failed = 6,
    Cancelled = 7
}

public sealed record KargonomiShipmentEvent(
    Guid Id,
    string ExternalStatus,
    string StatusLabel,
    KargonomiShipmentState State,
    string? TrackingNumber,
    DateTimeOffset OccurredAt,
    string? Description);

public sealed class KargonomiShipment
{
    private readonly List<KargonomiShipmentEvent> _events = [];

    private KargonomiShipment() { }

    private KargonomiShipment(Guid id, Guid orderId, Guid studentId, DateTimeOffset createdAt)
    {
        Id = id;
        OrderId = orderId;
        StudentId = studentId;
        State = KargonomiShipmentState.Pending;
        StatusLabel = "Başlatılmadı";
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid StudentId { get; private set; }
    public int? ExternalShipmentId { get; private set; }
    public string Carrier { get; private set; } = "Aras Kargo";
    public string? TrackingNumber { get; private set; }
    public string? ExternalStatus { get; private set; }
    public string StatusLabel { get; private set; } = string.Empty;
    public KargonomiShipmentState State { get; private set; }
    public string? BarcodeBase64 { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<KargonomiShipmentEvent> Events => _events.AsReadOnly();

    public static KargonomiShipment Create(Guid id, Guid orderId, Guid studentId, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || orderId == Guid.Empty || studentId == Guid.Empty)
            throw new DomainException("kargonomi_shipment.required_fields", "Sipariş ve öğrenci bilgileri zorunludur.");

        return new KargonomiShipment(id, orderId, studentId, createdAt);
    }

    public void MarkCreated(int externalShipmentId, string? externalStatus, string statusLabel,
        string? trackingNumber, DateTimeOffset occurredAt)
    {
        if (externalShipmentId <= 0)
            throw new DomainException("kargonomi_shipment.external_id_required", "Kargonomi gönderi kimliği zorunludur.");

        ExternalShipmentId = externalShipmentId;
        ExternalStatus = Clean(externalStatus, 80);
        StatusLabel = Clean(statusLabel, 160) ?? "Taslak";
        TrackingNumber = Clean(trackingNumber, 160);
        State = MapState(ExternalStatus, StatusLabel);
        UpdatedAt = occurredAt;
        LastError = null;
    }

    public void ApplyUpdate(string? externalStatus, string? statusLabel, string? trackingNumber,
        DateTimeOffset occurredAt, string? description = null)
    {
        var normalizedStatus = Clean(externalStatus, 80);
        var normalizedLabel = Clean(statusLabel, 160) ?? StatusLabel;
        var normalizedTracking = Clean(trackingNumber, 160) ?? TrackingNumber;
        var normalizedDescription = Clean(description, 1000);
        var state = MapState(normalizedStatus, normalizedLabel);
        var lastEvent = _events.LastOrDefault();
        if (lastEvent is not null && lastEvent.ExternalStatus == (normalizedStatus ?? string.Empty) &&
            lastEvent.StatusLabel == normalizedLabel && lastEvent.State == state &&
            lastEvent.TrackingNumber == normalizedTracking && lastEvent.Description == normalizedDescription)
            return;

        ExternalStatus = normalizedStatus;
        StatusLabel = normalizedLabel;
        TrackingNumber = normalizedTracking;
        State = state;
        UpdatedAt = occurredAt;
        LastError = null;
        _events.Add(new KargonomiShipmentEvent(Guid.NewGuid(), ExternalStatus ?? string.Empty, StatusLabel,
            State, TrackingNumber, occurredAt, normalizedDescription));
    }

    public void MarkFailed(string error, DateTimeOffset occurredAt)
    {
        State = KargonomiShipmentState.Failed;
        StatusLabel = "Hata";
        LastError = Clean(error, 2000) ?? "Bilinmeyen Kargonomi hatası.";
        UpdatedAt = occurredAt;
    }

    public void SetBarcode(string barcodeBase64, DateTimeOffset occurredAt)
    {
        BarcodeBase64 = string.IsNullOrWhiteSpace(barcodeBase64) ? null : barcodeBase64.Trim();
        UpdatedAt = occurredAt;
    }

    private static string? Clean(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var cleaned = value.Trim();
        return cleaned.Length <= maximumLength ? cleaned : cleaned[..maximumLength];
    }

    private static KargonomiShipmentState MapState(string? status, string? statusLabel) =>
        status?.Trim().ToLowerInvariant() switch
    {
        "draft" => KargonomiShipmentState.Draft,
        "ready" or "processing" or "prepared" or "webservice_order_creating" or
            "webservice_order_created" or "webservice_checking_shipment" => KargonomiShipmentState.Ready,
        "shipped" or "in_transit" or "in-transit" or "on_the_way" or
            "webservice_shipment_started" => KargonomiShipmentState.InTransit,
        "delivered" or "completed" or "webservice_shipment_delivered" => KargonomiShipmentState.Delivered,
        "cancelled" or "canceled" or "request_for_cancellation" => KargonomiShipmentState.Cancelled,
        "webservice_order_failed" or "webservice_shipment_not_delivered" or
            "webservice_shipment_missing" => KargonomiShipmentState.Failed,
        _ => MapLabelState(statusLabel)
    };

    private static KargonomiShipmentState MapLabelState(string? statusLabel) => statusLabel?.Trim().ToLowerInvariant() switch
    {
        "işleme hazır" or "kargo siparişi oluşturuluyor" or "kargo siparişi oluşturuldu" or
            "kargo kaydı kontrol ediliyor" => KargonomiShipmentState.Ready,
        "teslim sürecinde" or "kargo teslim sürecinde" => KargonomiShipmentState.InTransit,
        "teslim edildi" or "kargo teslim edildi" => KargonomiShipmentState.Delivered,
        "kargo sipariş oluşturulamadı" or "kargo teslim edilemedi" or "kargo kayıp" => KargonomiShipmentState.Failed,
        "kargo iptal edildi" or "iptal talebi alındı" => KargonomiShipmentState.Cancelled,
        _ => KargonomiShipmentState.Pending
    };
}
