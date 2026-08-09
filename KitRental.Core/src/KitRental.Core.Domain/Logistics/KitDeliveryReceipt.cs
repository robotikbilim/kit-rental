using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Logistics;

public sealed class KitDeliveryReceipt
{
    private KitDeliveryReceipt()
    {
    }

    private KitDeliveryReceipt(Guid id, Guid productUnitId, Guid assignmentId, Guid orderId,
        Guid customerId, string recipientFirstName, string recipientLastName, string recipientPhone,
        string addressLine, string district, string city, DateTimeOffset receivedAt, Guid actorId,
        double? latitude, double? longitude)
    {
        Id = id;
        ProductUnitId = productUnitId;
        AssignmentId = assignmentId;
        OrderId = orderId;
        CustomerId = customerId;
        RecipientFirstName = recipientFirstName;
        RecipientLastName = recipientLastName;
        RecipientPhone = recipientPhone;
        AddressLine = addressLine;
        District = district;
        City = city;
        ReceivedAt = receivedAt;
        ActorId = actorId;
        Latitude = latitude;
        Longitude = longitude;
    }

    public Guid Id { get; private set; }
    public Guid ProductUnitId { get; private set; }
    public Guid AssignmentId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string RecipientFirstName { get; private set; } = string.Empty;
    public string RecipientLastName { get; private set; } = string.Empty;
    public string RecipientPhone { get; private set; } = string.Empty;
    public string AddressLine { get; private set; } = string.Empty;
    public string District { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; private set; }
    public Guid ActorId { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }

    public string RecipientFullName => $"{RecipientFirstName} {RecipientLastName}".Trim();

    public static KitDeliveryReceipt Create(Guid id, Guid productUnitId, Guid assignmentId, Guid orderId,
        Guid customerId, string recipientFirstName, string recipientLastName, string recipientPhone,
        string addressLine, string district, string city, DateTimeOffset receivedAt, Guid actorId,
        double? latitude = null, double? longitude = null)
    {
        if (id == Guid.Empty || productUnitId == Guid.Empty || assignmentId == Guid.Empty ||
            orderId == Guid.Empty || customerId == Guid.Empty || actorId == Guid.Empty)
            throw new DomainException("kit_delivery.id_required", "Teslim kaydi icin kimlik bilgileri zorunludur.");

        if (string.IsNullOrWhiteSpace(recipientFirstName) || string.IsNullOrWhiteSpace(recipientLastName) ||
            string.IsNullOrWhiteSpace(recipientPhone) || string.IsNullOrWhiteSpace(addressLine) ||
            string.IsNullOrWhiteSpace(district) || string.IsNullOrWhiteSpace(city))
            throw new DomainException("kit_delivery.contact_required", "Teslim alan kisi, telefon ve adres zorunludur.");

        return new KitDeliveryReceipt(id, productUnitId, assignmentId, orderId, customerId,
            recipientFirstName.Trim(), recipientLastName.Trim(), recipientPhone.Trim(), addressLine.Trim(),
            district.Trim(), city.Trim(), receivedAt, actorId, latitude, longitude);
    }
}
