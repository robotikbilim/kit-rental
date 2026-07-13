namespace KitRental.Core.Domain.Auditing;

public sealed record AuditEntry(
    Guid Id,
    Guid ActorId,
    string EntityType,
    Guid EntityId,
    string Action,
    string? PreviousValue,
    string? NewValue,
    DateTimeOffset OccurredAt);
