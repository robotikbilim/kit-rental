using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Support;

public enum FaultSeverity { Low = 1, Medium = 2, High = 3, Critical = 4 }
// Values 1-8 are retained for existing records. New records use the explicit repair workflow below.
public enum FaultStatus
{
    Open = 1, Investigating = 2, WaitingForCustomer = 3, AwaitingReturn = 4, InService = 5,
    ReplacementInTransit = 6, Resolved = 7, Closed = 8,
    Accepted = 9, Rejected = 10, RemoteResolved = 11, AwaitingWorkshopShipment = 12,
    WorkshopShipmentInTransit = 13, WorkshopReceived = 14, Repaired = 15, CustomerShipmentInTransit = 16
}
public enum FaultApprovalStatus { NotRequired = 0, PendingCustomerApproval = 1, Approved = 2, Rejected = 3 }
public enum FaultOrigin { Internal = 1, PublicForm = 2, CustomerPortal = 3 }
public sealed record FaultStatusEvent(Guid Id, FaultStatus Previous, FaultStatus Current, DateTimeOffset OccurredAt, Guid ActorId, string Note);

public sealed class FaultTicket
{
    private readonly List<FaultStatusEvent> _history = [];
    private readonly List<FaultKargonomiShipment> _kargonomiShipments = [];
    private FaultTicket() { }
    private FaultTicket(Guid id, string number, Guid customerId, Guid orderId, Guid assignmentId, Guid productUnitId,
        string category, FaultSeverity severity, string description, DateTimeOffset openedAt, string reporterName,
        string reporterPhone, string reporterAddress, double? latitude, double? longitude, FaultOrigin origin,
        string? attachmentUrl)
    {
        Id = id; Number = number; CustomerId = customerId; OrderId = orderId; AssignmentId = assignmentId; ProductUnitId = productUnitId;
        Category = category; Severity = severity; Description = description; OpenedAt = openedAt; Status = FaultStatus.Open;
        ReporterName = reporterName; ReporterPhone = reporterPhone; ReporterAddress = reporterAddress;
        Latitude = latitude; Longitude = longitude;
        ApprovalStatus = FaultApprovalStatus.NotRequired;
        Origin = origin;
        AttachmentUrl = attachmentUrl?.Trim();
    }

    public Guid Id { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid AssignmentId { get; private set; }
    public Guid ProductUnitId { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public FaultSeverity Severity { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string ReporterName { get; private set; } = string.Empty;
    public string ReporterPhone { get; private set; } = string.Empty;
    public string ReporterAddress { get; private set; } = string.Empty;
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public FaultApprovalStatus ApprovalStatus { get; private set; }
    public FaultOrigin Origin { get; private set; } = FaultOrigin.Internal;
    public string? AttachmentUrl { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public FaultStatus Status { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public IReadOnlyCollection<FaultStatusEvent> History => _history.AsReadOnly();
    public IReadOnlyCollection<FaultKargonomiShipment> KargonomiShipments => _kargonomiShipments.AsReadOnly();

    public FaultKargonomiShipment CreateKargonomiShipment(FaultKargonomiShipmentDirection direction,
        string recipientName, string recipientPhone, string recipientAddress, DateTimeOffset now)
    {
        var shipment = FaultKargonomiShipment.Create(Guid.NewGuid(), Id, direction, recipientName, recipientPhone, recipientAddress, now);
        _kargonomiShipments.Add(shipment);
        return shipment;
    }

    public void EnsureCanStartShipment(FaultKargonomiShipmentDirection direction)
    {
        var allowed = (direction is FaultKargonomiShipmentDirection.ToWorkshop or FaultKargonomiShipmentDirection.ToCustomer) &&
            (Status is FaultStatus.Accepted or FaultStatus.AwaitingWorkshopShipment or FaultStatus.WorkshopShipmentInTransit
                or FaultStatus.WorkshopReceived or FaultStatus.Repaired or FaultStatus.CustomerShipmentInTransit);
        if (!allowed)
            throw new DomainException("fault.invalid_shipment_stage", "Kargo işlemi için arızanın incelenip kabul edilmiş ve uzaktan çözülmemiş olması gerekir.");
    }

    public static FaultTicket Open(Guid id, string number, Guid customerId, Guid orderId, Guid assignmentId,
        Guid productUnitId, string category, FaultSeverity severity, string description, DateTimeOffset openedAt,
        string? reporterName = null, string? reporterPhone = null, string? reporterAddress = null,
        double? latitude = null, double? longitude = null, FaultOrigin origin = FaultOrigin.Internal,
        string? attachmentUrl = null)
    {
        if (new[] { id, customerId, orderId, assignmentId, productUnitId }.Any(value => value == Guid.Empty) || string.IsNullOrWhiteSpace(description))
            throw new DomainException("fault.required_fields", "Arıza için müşteri, sipariş, atama, ürün ve açıklama zorunludur.");
        return new FaultTicket(id, number, customerId, orderId, assignmentId, productUnitId, category.Trim(),
            severity, description.Trim(), openedAt, reporterName?.Trim() ?? string.Empty,
            TurkishPhoneNumber.NormalizeOptional(reporterPhone, "Bildiren telefon numarası"), reporterAddress?.Trim() ?? string.Empty,
            latitude, longitude, origin, attachmentUrl);
    }

    public void ChangeStatus(FaultStatus next, Guid actorId, DateTimeOffset now, string note)
    {
        if (actorId == Guid.Empty || string.IsNullOrWhiteSpace(note) || next == Status)
            throw new DomainException("fault.invalid_status_change", "Arıza durum değişikliği için yeni durum, aktör ve not zorunludur.");
        var previous = Status;
        Status = next;
        _history.Add(new FaultStatusEvent(Guid.NewGuid(), previous, next, now, actorId, note.Trim()));
    }

    public void MarkInvestigating(Guid actorId, DateTimeOffset now, string note) =>
        MoveTo(FaultStatus.Investigating, actorId, now, note, FaultStatus.Open);

    public void Accept(Guid actorId, DateTimeOffset now, string note)
    {
        EnsureStatus(FaultStatus.Investigating);
        EnsureChangeInputs(actorId, note);
        ApprovalStatus = FaultApprovalStatus.Approved;
        ApprovedAt = now;
        MoveTo(FaultStatus.Accepted, actorId, now, note, FaultStatus.Investigating);
    }

    public void Reject(Guid actorId, DateTimeOffset now, string note)
    {
        EnsureStatus(FaultStatus.Investigating);
        EnsureChangeInputs(actorId, note);
        ApprovalStatus = FaultApprovalStatus.Rejected;
        MoveTo(FaultStatus.Rejected, actorId, now, note, FaultStatus.Investigating);
    }

    public void ResolveRemotely(Guid actorId, DateTimeOffset now, string note) =>
        MoveTo(FaultStatus.RemoteResolved, actorId, now, note, FaultStatus.Accepted);

    public void AwaitWorkshopShipment(Guid actorId, DateTimeOffset now, string note) =>
        MoveTo(FaultStatus.AwaitingWorkshopShipment, actorId, now, note, FaultStatus.Accepted);

    public void MarkWorkshopShipmentInTransit(Guid actorId, DateTimeOffset now, string note) =>
        MoveTo(FaultStatus.WorkshopShipmentInTransit, actorId, now, note, FaultStatus.Accepted, FaultStatus.AwaitingWorkshopShipment);

    public void MarkWorkshopReceived(Guid actorId, DateTimeOffset now, string note) =>
        MoveTo(FaultStatus.WorkshopReceived, actorId, now, note, FaultStatus.WorkshopShipmentInTransit);

    public void MarkRepaired(Guid actorId, DateTimeOffset now, string note) =>
        MoveTo(FaultStatus.Repaired, actorId, now, note, FaultStatus.WorkshopReceived);

    public void MarkCustomerShipmentInTransit(Guid actorId, DateTimeOffset now, string note) =>
        MoveTo(FaultStatus.CustomerShipmentInTransit, actorId, now, note, FaultStatus.Accepted,
            FaultStatus.AwaitingWorkshopShipment, FaultStatus.WorkshopShipmentInTransit, FaultStatus.WorkshopReceived, FaultStatus.Repaired);

    public void Close(Guid actorId, DateTimeOffset now, string note) =>
        MoveTo(FaultStatus.Closed, actorId, now, note, FaultStatus.Repaired, FaultStatus.RemoteResolved, FaultStatus.CustomerShipmentInTransit);

    private void MoveTo(FaultStatus next, Guid actorId, DateTimeOffset now, string note, params FaultStatus[] allowed)
    {
        if (!allowed.Contains(Status))
            throw new DomainException("fault.invalid_workflow_transition", "Arıza bu aşamadan seçilen işleme geçirilemez.");
        ChangeStatus(next, actorId, now, note);
    }

    private void EnsureStatus(FaultStatus expected)
    {
        if (Status != expected)
            throw new DomainException("fault.invalid_workflow_transition", "Arıza bu aşamadan onaylanamaz.");
    }

    private static void EnsureChangeInputs(Guid actorId, string note)
    {
        if (actorId == Guid.Empty || string.IsNullOrWhiteSpace(note))
            throw new DomainException("fault.invalid_status_change", "Arıza durumu değişikliği için aktör ve not zorunludur.");
    }
    public void UpdatePublicDetails(string category, string description, string reporterName,
        string reporterPhone, string reporterAddress, double? latitude, double? longitude, string? attachmentUrl = null)
    {
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(description) ||
            string.IsNullOrWhiteSpace(reporterName) || string.IsNullOrWhiteSpace(reporterPhone) ||
            string.IsNullOrWhiteSpace(reporterAddress))
            throw new DomainException("fault.required_fields", "Arıza için bildiren kişi, telefon, adres, kategori ve açıklama zorunludur.");

        Category = category.Trim();
        Description = description.Trim();
        ReporterName = reporterName.Trim();
        ReporterPhone = TurkishPhoneNumber.Normalize(reporterPhone, "Bildiren telefon numarası");
        ReporterAddress = reporterAddress.Trim();
        Latitude = latitude;
        Longitude = longitude;
        AttachmentUrl = attachmentUrl?.Trim();
    }
}
