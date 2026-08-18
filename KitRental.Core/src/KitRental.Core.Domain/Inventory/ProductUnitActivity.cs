using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Inventory;

public sealed class ProductUnitActivity
{
    private ProductUnitActivity()
    {
    }

    private ProductUnitActivity(Guid id, Guid productUnitId, Guid? assignmentId, Guid? orderId, Guid? studentId,
        Guid actorId, string actorDisplayNameSnapshot, string action, string description, DateTimeOffset occurredAt)
    {
        Id = id;
        ProductUnitId = productUnitId;
        AssignmentId = assignmentId;
        OrderId = orderId;
        StudentId = studentId;
        ActorId = actorId;
        ActorDisplayNameSnapshot = actorDisplayNameSnapshot.Trim();
        Action = action.Trim();
        Description = description.Trim();
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }
    public Guid ProductUnitId { get; private set; }
    public Guid? AssignmentId { get; private set; }
    public Guid? OrderId { get; private set; }
    public Guid? StudentId { get; private set; }
    public Guid ActorId { get; private set; }
    public string ActorDisplayNameSnapshot { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }

    public static ProductUnitActivity Create(Guid id, Guid productUnitId, Guid? assignmentId, Guid? orderId,
        Guid? studentId, Guid actorId, string actorDisplayNameSnapshot, string action, string description,
        DateTimeOffset occurredAt)
    {
        if (id == Guid.Empty || productUnitId == Guid.Empty || actorId == Guid.Empty ||
            string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(description))
            throw new DomainException("product_unit_activity.required_fields",
                "Kit işlem geçmişi için kit, kullanıcı, işlem ve açıklama zorunludur.");

        var actorName = string.IsNullOrWhiteSpace(actorDisplayNameSnapshot)
            ? actorId.ToString()
            : actorDisplayNameSnapshot;
        return new ProductUnitActivity(id, productUnitId, assignmentId, orderId, studentId, actorId,
            actorName, action, description, occurredAt);
    }
}
