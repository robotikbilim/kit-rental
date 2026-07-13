using KitRental.Core.Domain.Customers;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Rentals;
using KitRental.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace KitRental.Core.IntegrationTests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void ClientGeneratedOwnedEntityIds_AreNeverDatabaseGenerated()
    {
        var options = new DbContextOptionsBuilder<KitRentalDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=KitRentalModelMetadata;Trusted_Connection=True")
            .Options;
        using var context = new KitRentalDbContext(options);

        var ownedIdProperties = context.Model.GetEntityTypes()
            .Where(entityType => entityType.IsOwned())
            .Select(entityType => new
            {
                EntityType = entityType.DisplayName(),
                Id = entityType.FindProperty("Id")
            })
            .Where(item => item.Id is not null)
            .ToArray();

        Assert.NotEmpty(ownedIdProperties);
        Assert.All(ownedIdProperties, item =>
            Assert.Equal(ValueGenerated.Never, item.Id!.ValueGenerated));
    }

    [Fact]
    public void NewOrderStatusEvent_OnTrackedOrder_IsMarkedAsAdded()
    {
        var options = new DbContextOptionsBuilder<KitRentalDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=KitRentalModelMetadata;Trusted_Connection=True")
            .Options;
        using var context = new KitRentalDbContext(options);
        var actorId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var order = RentalOrder.Create(
            Guid.NewGuid(),
            "TEST-ORDER-1",
            Guid.NewGuid(),
            new RentalPeriod(DateOnly.FromDateTime(now.Date), DateOnly.FromDateTime(now.Date.AddDays(7))),
            new AddressSnapshot("Test", "555", "Adres", "İlçe", "Şehir", "34000"),
            now);
        order.Submit(actorId, now);
        context.Attach(order);

        order.Approve(actorId, now.AddMinutes(1));
        context.ChangeTracker.DetectChanges();

        var approvalEvent = order.History.Single(item => item.Current == RentalOrderStatus.Approved);
        Assert.Equal(EntityState.Added, context.Entry(approvalEvent).State);
    }
}
