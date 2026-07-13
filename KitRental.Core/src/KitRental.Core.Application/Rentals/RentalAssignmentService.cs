using KitRental.Core.Application.Abstractions;
using KitRental.Core.Application.Common;
using KitRental.Core.Domain.Auditing;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Rentals;

namespace KitRental.Core.Application.Rentals;

public sealed class RentalAssignmentService(ICoreRepository repository, TimeProvider timeProvider)
{
    public async Task<RentalAssignmentResponse> CreateAsync(
        CreateRentalAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        var unit = await repository.GetProductUnitAsync(command.ProductUnitId, cancellationToken)
            ?? throw new ResourceNotFoundException("Fiziksel ürün birimi bulunamadı.");
        var order = await repository.FindOrderByLineIdAsync(command.OrderLineId, cancellationToken)
            ?? throw new ResourceNotFoundException("Sipariş satırı bulunamadı.");

        if (order.CustomerId != command.CustomerId)
        {
            throw new ForbiddenException("Sipariş satırı belirtilen müşteriye ait değil.");
        }

        if (order.Status != RentalOrderStatus.Approved)
        {
            throw new ConflictException("rental_assignment.order_not_approved", "Fiziksel ürün yalnız onaylanmış siparişe atanabilir.");
        }

        if (order.Period != new RentalPeriod(command.StartDate, command.EndDate))
        {
            throw new ConflictException("rental_assignment.period_mismatch", "Atama tarihleri sipariş tarihleriyle eşleşmelidir.");
        }

        if (unit.Status is ProductUnitStatus.InMaintenance or ProductUnitStatus.Quarantined or ProductUnitStatus.Lost or ProductUnitStatus.Retired)
        {
            throw new ConflictException("rental_assignment.unit_not_rentable", "Ürün birimi mevcut durumunda kiralanamaz.");
        }

        var now = timeProvider.GetUtcNow();
        var assignment = RentalAssignment.Create(
            Guid.NewGuid(),
            command.OrderLineId,
            command.CustomerId,
            command.ProductUnitId,
            new RentalPeriod(command.StartDate, command.EndDate),
            now,
            command.ActorId);

        var created = await repository.TryCreateReservationAsync(
            unit,
            assignment,
            command.ActorId,
            now,
            cancellationToken);

        if (!created)
        {
            throw new ConflictException(
                "rental_assignment.period_overlap",
                "Fiziksel ürün birimi seçilen tarih aralığında başka bir aktif atamaya sahiptir.");
        }

        await repository.AddAuditEntryAsync(
            new AuditEntry(Guid.NewGuid(), command.ActorId, nameof(RentalAssignment), assignment.Id, "Reserved", null,
                $"{assignment.Period.StartDate:O}/{assignment.Period.EndDate:O}", now),
            cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return new RentalAssignmentResponse(
            assignment.Id,
            assignment.OrderLineId,
            assignment.CustomerId,
            assignment.ProductUnitId,
            assignment.Period.StartDate,
            assignment.Period.EndDate,
            assignment.Status);
    }
}
