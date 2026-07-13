using KitRental.Core.Domain.Rentals;
using KitRental.SharedKernel;

namespace KitRental.Core.UnitTests;

public sealed class RentalPeriodTests
{
    [Fact]
    public void Overlaps_ReturnsTrue_WhenPeriodsShareAnyDay()
    {
        var first = new RentalPeriod(new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 15));
        var second = new RentalPeriod(new DateOnly(2026, 7, 15), new DateOnly(2026, 7, 20));

        Assert.True(first.Overlaps(second));
    }

    [Fact]
    public void Overlaps_ReturnsFalse_WhenPeriodsAreSeparate()
    {
        var first = new RentalPeriod(new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 14));
        var second = new RentalPeriod(new DateOnly(2026, 7, 15), new DateOnly(2026, 7, 20));

        Assert.False(first.Overlaps(second));
    }

    [Fact]
    public void Constructor_RejectsEndDateBeforeStartDate()
    {
        var exception = Assert.Throws<DomainException>(() =>
            new RentalPeriod(new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 10)));

        Assert.Equal("rental_period.invalid", exception.Code);
    }
}
