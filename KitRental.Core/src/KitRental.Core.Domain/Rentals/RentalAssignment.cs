using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Rentals;

public enum RentalAssignmentStatus
{
    Reserved = 1,
    Active = 2,
    Completed = 3,
    Cancelled = 4
}

public sealed class RentalAssignment
{
    private RentalAssignment()
    {
    }

    private RentalAssignment(
        Guid id,
        Guid orderLineId,
        Guid customerId,
        Guid productUnitId,
        RentalPeriod period,
        DateTimeOffset createdAt,
        Guid createdBy)
    {
        Id = id;
        OrderLineId = orderLineId;
        CustomerId = customerId;
        ProductUnitId = productUnitId;
        Period = period;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        Status = RentalAssignmentStatus.Reserved;
    }

    public Guid Id { get; private set; }
    public Guid OrderLineId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid ProductUnitId { get; private set; }
    public RentalPeriod Period { get; private set; }
    public RentalAssignmentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    public bool BlocksAvailability => Status is RentalAssignmentStatus.Reserved or RentalAssignmentStatus.Active;

    public static RentalAssignment Create(
        Guid id,
        Guid orderLineId,
        Guid customerId,
        Guid productUnitId,
        RentalPeriod period,
        DateTimeOffset createdAt,
        Guid createdBy)
    {
        if (new[] { id, orderLineId, customerId, productUnitId, createdBy }.Any(value => value == Guid.Empty))
        {
            throw new DomainException("rental_assignment.required_ids", "Kiralama atamasındaki kimlik alanları zorunludur.");
        }

        return new RentalAssignment(id, orderLineId, customerId, productUnitId, period, createdAt, createdBy);
    }

    public void Activate()
    {
        if (Status != RentalAssignmentStatus.Reserved)
            throw new DomainException("rental_assignment.invalid_status_transition", "Yalnızca rezerve kiralama aktifleştirilebilir.");
        Status = RentalAssignmentStatus.Active;
    }

    public void Complete()
    {
        if (Status != RentalAssignmentStatus.Active)
            throw new DomainException("rental_assignment.invalid_status_transition", "Yalnızca aktif kiralama tamamlanabilir.");
        Status = RentalAssignmentStatus.Completed;
    }
}
