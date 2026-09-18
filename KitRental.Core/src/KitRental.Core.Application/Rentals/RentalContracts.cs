using KitRental.Core.Domain.Rentals;

namespace KitRental.Core.Application.Rentals;

public sealed record CreateRentalAssignmentCommand(
    Guid OrderLineId,
    Guid CustomerId,
    Guid ProductUnitId,
    Guid ActorId);

public sealed record RentalAssignmentResponse(
    Guid Id,
    Guid OrderLineId,
    Guid CustomerId,
    Guid ProductUnitId,
    DateOnly StartDate,
    DateOnly EndDate,
    RentalAssignmentStatus Status);
