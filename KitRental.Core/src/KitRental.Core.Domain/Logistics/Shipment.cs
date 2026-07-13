using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Logistics;

public enum ShipmentType { Outbound = 1, Return = 2, ReplacementOutbound = 3, ReplacementReturn = 4 }
public enum ShipmentStatus { Created = 1, InTransit = 2, Delivered = 3, DeliveryIssue = 4, Cancelled = 5 }
public sealed record ShipmentEvent(Guid Id, ShipmentStatus Status, DateTimeOffset OccurredAt, string Location, string Description);

public sealed class Shipment
{
    private readonly List<ShipmentEvent> _events = [];
    private Shipment() { }
    private Shipment(Guid id, Guid orderId, Guid? faultTicketId, ShipmentType type, string carrier, string trackingNumber)
    {
        Id = id; OrderId = orderId; FaultTicketId = faultTicketId; Type = type; Carrier = carrier; TrackingNumber = trackingNumber;
        Status = ShipmentStatus.Created;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid? FaultTicketId { get; private set; }
    public ShipmentType Type { get; private set; }
    public string Carrier { get; private set; } = string.Empty;
    public string TrackingNumber { get; private set; } = string.Empty;
    public ShipmentStatus Status { get; private set; }
    public IReadOnlyCollection<ShipmentEvent> Events => _events.AsReadOnly();

    public static Shipment Create(Guid id, Guid orderId, Guid? faultTicketId, ShipmentType type, string carrier, string trackingNumber)
    {
        if (id == Guid.Empty || orderId == Guid.Empty || string.IsNullOrWhiteSpace(carrier) || string.IsNullOrWhiteSpace(trackingNumber))
            throw new DomainException("shipment.required_fields", "Kargo tipi, taşıyıcı ve takip numarası zorunludur.");
        return new Shipment(id, orderId, faultTicketId, type, carrier.Trim(), trackingNumber.Trim().ToUpperInvariant());
    }

    public ShipmentEvent AddEvent(ShipmentStatus status, DateTimeOffset occurredAt, string location, string description)
    {
        if (status == ShipmentStatus.Created || string.IsNullOrWhiteSpace(description))
            throw new DomainException("shipment.invalid_event", "Geçerli bir kargo olayı ve açıklama zorunludur.");
        Status = status;
        var shipmentEvent = new ShipmentEvent(Guid.NewGuid(), status, occurredAt, location.Trim(), description.Trim());
        _events.Add(shipmentEvent);
        return shipmentEvent;
    }
}
