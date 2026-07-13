namespace KitRental.Contracts;

public sealed record ShipmentStatusChanged(
    Guid ShipmentId,
    Guid OrderId,
    string Status,
    DateTimeOffset OccurredAt,
    string CorrelationId);

public sealed record RentalStatusChanged(
    Guid OrderId,
    Guid CustomerId,
    string PreviousStatus,
    string CurrentStatus,
    DateTimeOffset OccurredAt,
    string CorrelationId);

public sealed record FaultTicketOpened(
    Guid FaultTicketId,
    Guid CustomerId,
    Guid ProductUnitId,
    string Severity,
    DateTimeOffset OpenedAt,
    string CorrelationId);

public sealed record NotificationRequested(
    Guid RecipientUserId,
    string Template,
    IReadOnlyDictionary<string, string> Parameters,
    string CorrelationId);
