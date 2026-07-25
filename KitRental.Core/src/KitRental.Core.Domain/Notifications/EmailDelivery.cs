namespace KitRental.Core.Domain.Notifications;

public enum EmailDeliveryStatus
{
    Sent = 1,
    Failed = 2
}

public sealed class EmailDelivery
{
    private EmailDelivery() { }

    private EmailDelivery(Guid id, string recipient, string recipientName, string subject, string body,
        EmailDeliveryStatus status, DateTimeOffset occurredAt, string? error)
    {
        Id = id;
        Recipient = recipient;
        RecipientName = recipientName;
        Subject = subject;
        Body = body;
        Status = status;
        OccurredAt = occurredAt;
        Error = error;
    }

    public Guid Id { get; private set; }
    public string Recipient { get; private set; } = string.Empty;
    public string RecipientName { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public EmailDeliveryStatus Status { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string? Error { get; private set; }

    public static EmailDelivery Create(string recipient, string recipientName, string subject, string body,
        EmailDeliveryStatus status, DateTimeOffset occurredAt, string? error = null) =>
        new(Guid.NewGuid(), recipient.Trim(), recipientName.Trim(), subject.Trim(), body, status, occurredAt,
            string.IsNullOrWhiteSpace(error) ? null : error);
}
