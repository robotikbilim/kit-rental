using KitRental.Core.Domain.Customers;
using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Rentals;
using KitRental.Core.Domain.Returns;
using KitRental.Core.Domain.Support;
using KitRental.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace KitRental.Core.IntegrationTests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void EveryPersistedAddressStoresProviderIdsAndRegionLabelsSeparately()
    {
        var options = new DbContextOptionsBuilder<KitRentalDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=KitRentalModelMetadata;Trusted_Connection=True")
            .Options;
        using var context = new KitRentalDbContext(options);
        var addressTypes = new[]
        {
            typeof(Address), typeof(AddressSnapshot), typeof(RentalCohortStudent), typeof(KitLocationEvent),
            typeof(FaultTicket), typeof(FaultKargonomiShipment), typeof(KitReturnRequest)
        };

        foreach (var addressType in addressTypes)
        {
            var entity = Assert.Single(context.Model.GetEntityTypes(), item => item.ClrType == addressType);
            var store = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            var prefix = addressType == typeof(AddressSnapshot) ? "Delivery" : string.Empty;
            foreach (var name in new[] { "CityId", "DistrictId" })
            {
                var property = entity.FindProperty(name)!;
                Assert.NotNull(property);
                Assert.Equal(typeof(int?), property.ClrType);
                Assert.True(property.IsNullable);
                Assert.Equal(prefix + name, property.GetColumnName(store));
            }
            foreach (var name in new[] { "City", "District" })
            {
                var property = entity.FindProperty(name)!;
                Assert.NotNull(property);
                Assert.False(property.IsNullable);
                Assert.Equal(160, property.GetMaxLength());
                Assert.Equal(prefix + name, property.GetColumnName(store));
            }
        }
    }

    [Fact]
    public void ClientGeneratedOwnedEntityIdsAreNeverDatabaseGenerated()
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
    public void CustomerAllowedProductModelsGetDistinctClientGeneratedIds()
    {
        var customer = Customer.Create(Guid.NewGuid(), "Test Customer", "test@example.com");

        customer.SetAllowedProductModels([Guid.NewGuid(), Guid.NewGuid()]);

        var ids = customer.AllowedProductModels.Select(item => item.Id).ToArray();
        Assert.All(ids, id => Assert.NotEqual(Guid.Empty, id));
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void NewOrderStatusEventOnTrackedOrderIsMarkedAsAdded()
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
            new AddressSnapshot("Test", "555", "Adres", "34000"),
            now);
        order.Submit(actorId, now);
        context.Attach(order);

        order.Approve(actorId, now.AddMinutes(1));
        context.ChangeTracker.DetectChanges();

        var approvalEvent = order.History.Single(item => item.Current == RentalOrderStatus.Approved);
        Assert.Equal(EntityState.Added, context.Entry(approvalEvent).State);
    }
}
