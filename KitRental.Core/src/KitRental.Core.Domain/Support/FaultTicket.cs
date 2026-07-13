using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Support;

public enum FaultSeverity { Low = 1, Medium = 2, High = 3, Critical = 4 }
public enum FaultStatus { Open = 1, Investigating = 2, WaitingForCustomer = 3, AwaitingReturn = 4, InService = 5, ReplacementInTransit = 6, Resolved = 7, Closed = 8 }
public sealed record FaultStatusEvent(Guid Id, FaultStatus Previous, FaultStatus Current, DateTimeOffset OccurredAt, Guid ActorId, string Note);

public sealed class FaultTicket
{
    private readonly List<FaultStatusEvent> _history = [];
    private FaultTicket() { }
    private FaultTicket(Guid id, string number, Guid customerId, Guid orderId, Guid assignmentId, Guid productUnitId, string category, FaultSeverity severity, string description, DateTimeOffset openedAt)
    {
        Id = id; Number = number; CustomerId = customerId; OrderId = orderId; AssignmentId = assignmentId; ProductUnitId = productUnitId;
        Category = category; Severity = severity; Description = description; OpenedAt = openedAt; Status = FaultStatus.Open;
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
    public FaultStatus Status { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public IReadOnlyCollection<FaultStatusEvent> History => _history.AsReadOnly();

    public static FaultTicket Open(Guid id, string number, Guid customerId, Guid orderId, Guid assignmentId, Guid productUnitId, string category, FaultSeverity severity, string description, DateTimeOffset openedAt)
    {
        if (new[] { id, customerId, orderId, assignmentId, productUnitId }.Any(value => value == Guid.Empty) || string.IsNullOrWhiteSpace(description))
            throw new DomainException("fault.required_fields", "Arıza için müşteri, sipariş, atama, ürün ve açıklama zorunludur.");
        return new FaultTicket(id, number, customerId, orderId, assignmentId, productUnitId, category.Trim(), severity, description.Trim(), openedAt);
    }

    public void ChangeStatus(FaultStatus next, Guid actorId, DateTimeOffset now, string note)
    {
        if (actorId == Guid.Empty || string.IsNullOrWhiteSpace(note) || next == Status)
            throw new DomainException("fault.invalid_status_change", "Arıza durum değişikliği için yeni durum, aktör ve not zorunludur.");
        var previous = Status;
        Status = next;
        _history.Add(new FaultStatusEvent(Guid.NewGuid(), previous, next, now, actorId, note.Trim()));
    }
}
