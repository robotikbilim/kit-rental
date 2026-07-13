using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Rentals;

public readonly record struct RentalPeriod
{
    public RentalPeriod(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new DomainException("rental_period.invalid", "Kiralama bitiş tarihi başlangıç tarihinden önce olamaz.");
        }

        StartDate = startDate;
        EndDate = endDate;
    }

    public DateOnly StartDate { get; }
    public DateOnly EndDate { get; }

    public bool Overlaps(RentalPeriod other) =>
        StartDate <= other.EndDate && other.StartDate <= EndDate;
}
