using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Logistics;

public enum KitLocationEventSource
{
    DeliveryReceipt = 1,
    FaultReport = 2,
    FaultUpdate = 3,
    ReturnRequest = 4
}

public sealed class KitLocationEvent
{
    private KitLocationEvent()
    {
    }

    private KitLocationEvent(Guid id, Guid productUnitId, Guid? assignmentId, Guid? orderId,
        Guid? customerId, KitLocationEventSource source, Guid? sourceId, string contactName,
        string contactPhone, string addressLine, string district, string city, double? latitude,
        double? longitude, DateTimeOffset occurredAt, Guid actorId)
    {
        Id = id;
        ProductUnitId = productUnitId;
        AssignmentId = assignmentId;
        OrderId = orderId;
        CustomerId = customerId;
        Source = source;
        SourceId = sourceId;
        ContactName = contactName;
        ContactPhone = contactPhone;
        AddressLine = addressLine;
        District = district;
        City = city;
        Latitude = latitude;
        Longitude = longitude;
        OccurredAt = occurredAt;
        ActorId = actorId;
    }

    public Guid Id { get; private set; }
    public Guid ProductUnitId { get; private set; }
    public Guid? AssignmentId { get; private set; }
    public Guid? OrderId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public KitLocationEventSource Source { get; private set; }
    public Guid? SourceId { get; private set; }
    public string ContactName { get; private set; } = string.Empty;
    public string ContactPhone { get; private set; } = string.Empty;
    public string AddressLine { get; private set; } = string.Empty;
    public string District { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public Guid ActorId { get; private set; }

    public static KitLocationEvent Create(Guid id, Guid productUnitId, Guid? assignmentId, Guid? orderId,
        Guid? customerId, KitLocationEventSource source, Guid? sourceId, string contactName,
        string contactPhone, string addressLine, string district, string city, double? latitude,
        double? longitude, DateTimeOffset occurredAt, Guid actorId)
    {
        if (id == Guid.Empty || productUnitId == Guid.Empty || actorId == Guid.Empty)
            throw new DomainException("kit_location_event.id_required", "Kit, event and actor identities are required.");
        if (string.IsNullOrWhiteSpace(contactName) || string.IsNullOrWhiteSpace(addressLine))
            throw new DomainException("kit_location_event.location_required", "Contact name and address are required.");
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw new DomainException("kit_location_event.invalid_coordinates", "Coordinates are invalid.");

        return new KitLocationEvent(id, productUnitId, assignmentId, orderId, customerId, source, sourceId,
            contactName.Trim(), TurkishPhoneNumber.NormalizeOptional(contactPhone, "İletişim telefon numarası"), addressLine.Trim(),
            string.IsNullOrWhiteSpace(district) ? "Bilinmiyor" : district.Trim(),
            string.IsNullOrWhiteSpace(city) ? "Bilinmiyor" : city.Trim(),
            latitude, longitude, occurredAt, actorId);
    }
}
