using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Returns;

public enum KitReturnStatus { Requested = 1, InTransit = 2, Received = 3 }

public sealed record KitReturnItem(Guid Id, Guid AssignmentId, Guid ProductUnitId, Guid OrderId);

public sealed class KitReturnRequest
{
    private readonly List<KitReturnItem> _items = [];
    private KitReturnRequest() { }

    private KitReturnRequest(Guid id, Guid customerId, DateTimeOffset createdAt, Guid createdBy,
        IReadOnlyCollection<KitReturnItem> items, string? requesterName = null,
        string? requesterPhone = null, string? returnAddress = null, double? latitude = null, double? longitude = null)
    {
        Id = id; CustomerId = customerId; CreatedAt = createdAt; CreatedBy = createdBy;
        RequesterName = requesterName?.Trim();
        RequesterPhone = requesterPhone?.Trim();
        ReturnAddress = returnAddress?.Trim();
        Latitude = latitude;
        Longitude = longitude;
        Status = KitReturnStatus.Requested; _items.AddRange(items);
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public KitReturnStatus Status { get; private set; }
    public string? Carrier { get; private set; }
    public string? TrackingNumber { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ShippedAt { get; private set; }
    public DateTimeOffset? ReceivedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public string? RequesterName { get; private set; }
    public string? RequesterPhone { get; private set; }
    public string? ReturnAddress { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public IReadOnlyCollection<KitReturnItem> Items => _items.AsReadOnly();

    public static KitReturnRequest Create(Guid id, Guid customerId, DateTimeOffset createdAt, Guid createdBy,
        IReadOnlyCollection<KitReturnItem> items)
    {
        if (id == Guid.Empty || customerId == Guid.Empty || createdBy == Guid.Empty || items.Count == 0 ||
            items.Any(x => x.AssignmentId == Guid.Empty || x.ProductUnitId == Guid.Empty || x.OrderId == Guid.Empty))
            throw new DomainException("kit_return.required_fields", "İade için en az bir geçerli kit seçilmelidir.");
        if (items.Select(x => x.ProductUnitId).Distinct().Count() != items.Count)
            throw new DomainException("kit_return.duplicate_unit", "Aynı kit bir iadeye birden fazla eklenemez.");
        return new KitReturnRequest(id, customerId, createdAt, createdBy, items);
    }

    public static KitReturnRequest CreatePublic(Guid id, Guid customerId, DateTimeOffset createdAt, Guid createdBy,
        IReadOnlyCollection<KitReturnItem> items, string requesterName,
        string requesterPhone, string returnAddress, double? latitude, double? longitude)
    {
        if (string.IsNullOrWhiteSpace(requesterName) || string.IsNullOrWhiteSpace(requesterPhone) || string.IsNullOrWhiteSpace(returnAddress))
            throw new DomainException("kit_return.requester_required", "Ad, soyad ve telefon zorunludur.");
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw new DomainException("kit_return.invalid_location", "GeÃ§erli bir konum seÃ§ilmelidir.");
        var request = Create(id, customerId, createdAt, createdBy, items);
        return new KitReturnRequest(request.Id, request.CustomerId, request.CreatedAt, request.CreatedBy, request.Items,
            requesterName, requesterPhone, returnAddress, latitude, longitude);
    }

    public void MarkShipped(string carrier, string trackingNumber, DateTimeOffset shippedAt)
    {
        if (Status != KitReturnStatus.Requested || string.IsNullOrWhiteSpace(carrier) || string.IsNullOrWhiteSpace(trackingNumber))
            throw new DomainException("kit_return.invalid_shipment", "Kargo firması ve takip numarası zorunludur.");
        Carrier = carrier.Trim(); TrackingNumber = trackingNumber.Trim().ToUpperInvariant();
        ShippedAt = shippedAt; Status = KitReturnStatus.InTransit;
    }

    public void Receive(DateTimeOffset receivedAt)
    {
        if (Status != KitReturnStatus.InTransit)
            throw new DomainException("kit_return.invalid_receive", "Yalnızca kargoya verilmiş iadeler teslim alınabilir.");
        ReceivedAt = receivedAt; Status = KitReturnStatus.Received;
    }
}
