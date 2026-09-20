using KitRental.Core.Domain.Customers;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Rentals;

namespace KitRental.Core.UnitTests;

public sealed class RentalOrderTests
{
    [Fact]
    public void AddOneKitRequirementIncreasesExistingRentalLine()
    {
        var actorId = Guid.NewGuid();
        var productModelId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-09-20T12:00:00+03:00");
        var order = RentalOrder.Create(Guid.NewGuid(), "RR-20260920-TEST", Guid.NewGuid(),
            new RentalPeriod(new DateOnly(2026, 9, 20), new DateOnly(2026, 12, 31)),
            new AddressSnapshot("Test", "05550000000", "Test adres", "34000"), now);
        order.AddLine(productModelId, 1);
        order.Submit(actorId, now);

        order.AddOneKitRequirement(productModelId);

        Assert.Equal(2, Assert.Single(order.Lines).Quantity);
    }
}
