using KitRental.Core.Domain.Logistics;
using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Support;

public enum FaultKargonomiShipmentDirection { ToWorkshop = 1, ToCustomer = 2 }

public sealed record FaultKargonomiShipmentEvent(Guid Id, string ExternalStatus, string StatusLabel,
    KargonomiShipmentState State, string? TrackingNumber, DateTimeOffset OccurredAt, string? Description);

public sealed class FaultKargonomiShipment
{
    private readonly List<FaultKargonomiShipmentEvent> _events = [];
    private FaultKargonomiShipment() { }

    private FaultKargonomiShipment(Guid id, Guid faultTicketId, FaultKargonomiShipmentDirection direction,
        string recipientName, string recipientPhone, string recipientAddress, DateTimeOffset createdAt)
    {
        Id = id; FaultTicketId = faultTicketId; Direction = direction;
        RecipientName = recipientName; RecipientPhone = recipientPhone; RecipientAddress = recipientAddress;
        State = KargonomiShipmentState.Pending; StatusLabel = "Başlatılmadı";
        CreatedAt = createdAt; UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid FaultTicketId { get; private set; }
    public FaultKargonomiShipmentDirection Direction { get; private set; }
    public string RecipientName { get; private set; } = string.Empty;
    public string RecipientPhone { get; private set; } = string.Empty;
    public string RecipientAddress { get; private set; } = string.Empty;
    public int? ExternalShipmentId { get; private set; }
    public string Carrier { get; private set; } = "Aras Kargo";
    public string? TrackingNumber { get; private set; }
    public string? ExternalStatus { get; private set; }
    public string StatusLabel { get; private set; } = string.Empty;
    public KargonomiShipmentState State { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<FaultKargonomiShipmentEvent> Events => _events.AsReadOnly();

    public static FaultKargonomiShipment Create(Guid id, Guid faultTicketId, FaultKargonomiShipmentDirection direction,
        string recipientName, string recipientPhone, string recipientAddress, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || faultTicketId == Guid.Empty || string.IsNullOrWhiteSpace(recipientName) ||
            string.IsNullOrWhiteSpace(recipientPhone) || string.IsNullOrWhiteSpace(recipientAddress))
            throw new DomainException("fault_kargonomi.required_fields", "Arıza kargosu alıcı bilgileri zorunludur.");
        return new FaultKargonomiShipment(id, faultTicketId, direction, recipientName.Trim(), recipientPhone.Trim(),
            recipientAddress.Trim(), createdAt);
    }

    public void MarkCreated(int externalShipmentId, string? status, string statusLabel, string? trackingNumber,
        DateTimeOffset occurredAt, string? carrier = null)
    {
        if (externalShipmentId <= 0) throw new DomainException("fault_kargonomi.external_id_required", "Kargonomi gönderi kimliği zorunludur.");
        ExternalShipmentId = externalShipmentId; ExternalStatus = Clean(status); StatusLabel = Clean(statusLabel) ?? "Hazır";
        TrackingNumber = Clean(trackingNumber); State = MapState(status); UpdatedAt = occurredAt; LastError = null;
        Carrier = Clean(carrier) ?? Carrier;
    }

    public void ApplyUpdate(string? status, string? statusLabel, string? trackingNumber, DateTimeOffset occurredAt, string? description)
    {
        var normalizedStatus = Clean(status) ?? string.Empty; var label = Clean(statusLabel) ?? StatusLabel;
        var tracking = Clean(trackingNumber) ?? TrackingNumber; var state = MapState(status); var detail = Clean(description);
        var last = _events.LastOrDefault();
        if (last is not null && last.ExternalStatus == normalizedStatus && last.StatusLabel == label && last.State == state && last.TrackingNumber == tracking && last.Description == detail) return;
        ExternalStatus = normalizedStatus; StatusLabel = label; TrackingNumber = tracking; State = state; UpdatedAt = occurredAt; LastError = null;
        _events.Add(new FaultKargonomiShipmentEvent(Guid.NewGuid(), normalizedStatus, label, state, tracking, occurredAt, detail));
    }

    public void MarkFailed(string error, DateTimeOffset occurredAt) { State = KargonomiShipmentState.Failed; StatusLabel = "Hata"; LastError = error.Trim(); UpdatedAt = occurredAt; }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static KargonomiShipmentState MapState(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "draft" => KargonomiShipmentState.Draft, "ready" or "confirmed" or "processing" or "prepared" => KargonomiShipmentState.Ready,
        "shipped" or "in_transit" or "in-transit" or "on_the_way" => KargonomiShipmentState.InTransit,
        "delivered" or "completed" => KargonomiShipmentState.Delivered,
        "cancelled" or "canceled" => KargonomiShipmentState.Cancelled, _ => KargonomiShipmentState.Pending
    };
}
