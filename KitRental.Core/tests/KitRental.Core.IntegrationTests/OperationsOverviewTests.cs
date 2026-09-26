using KitRental.Core.Application.Inventory;
using KitRental.Core.Application.Operations;
using KitRental.Core.Application.CustomerPortal;
using KitRental.Core.Application.Kargonomi;
using KitRental.Core.Domain.Customers;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Rentals;
using KitRental.Core.Domain.Returns;
using KitRental.Core.Domain.Support;
using KitRental.Core.Infrastructure.Persistence;
using KitRental.SharedKernel;

namespace KitRental.Core.IntegrationTests;

public sealed class OperationsOverviewTests
{
    [Fact]
    public async Task CustomerPortalUsesAdminReturnCountsAndHidesOtherCustomers()
    {
        var fixture = await SeedAsync(new DateOnly(2026, 10, 1));
        await fixture.Repository.AddCustomerAsync(Customer.Create(Guid.NewGuid(), "Başka müşteri", "other@example.test"), Token);
        var request = NewReturn(fixture, Now);
        request.LinkExternalShipment(1234, "HepsiJet", "TRACK-IN", null, "delivered", "Teslim edildi", Now);
        await fixture.Repository.AddKitReturnRequestAsync(request, Token);
        var portal = new CustomerPortalService(fixture.Repository, fixture.Operations,
            new KargonomiShippingService(fixture.Repository, new FakeKargonomiClient(), new FixedTimeProvider()), fixture.Overview);

        var before = await portal.GetDashboardAsync(fixture.Customer.Id, Token);
        Assert.NotNull(before.Operations);
        Assert.Empty(before.Operations.Customers);
        Assert.Equal(fixture.Customer.Id, before.Operations.CustomerId);
        Assert.Equal(1, before.Operations.ReturnInTransitKitCount);
        Assert.Equal(0, before.Operations.ReturnCompletedKitCount);
        var beforeRows = await portal.GetReturnsPageAsync(fixture.Customer.Id, Token);
        Assert.Equal("in-transit", Assert.Single(beforeRows.OperationalReturns!).ReturnStateKey);

        request.Receive(Now);
        var after = await portal.GetDashboardAsync(fixture.Customer.Id, Token);
        var admin = await fixture.Overview.GetDashboardAsync(fixture.Customer.Id, Token);
        Assert.Equal(admin.ReturnCompletedKitCount, after.Operations!.ReturnCompletedKitCount);
        Assert.Equal(1, after.Operations.ReturnCompletedKitCount);
        Assert.Equal(0, after.Operations.ReturnInTransitKitCount);
        Assert.Equal(admin.ActiveKitCount, after.Operations.ActiveKitCount);
        var afterRows = await portal.GetReturnsPageAsync(fixture.Customer.Id, Token);
        Assert.Equal("completed", Assert.Single(afterRows.OperationalReturns!).ReturnStateKey);
        Assert.Equal("TRACK-IN", Assert.Single(afterRows.Returns).TrackingNumber);
    }

    [Fact]
    public async Task CustomerFaultGroupsAndIndependentShipmentsMatchAdmin()
    {
        var fixture = await SeedAsync(new DateOnly(2026, 10, 1));
        foreach (var status in new[] { FaultStatus.Open, FaultStatus.RemoteResolved, FaultStatus.Rejected, FaultStatus.CustomerShipmentInTransit })
        {
            var ticket = FaultTicket.Open(Guid.NewGuid(), $"FAULT-{status}", fixture.Customer.Id, fixture.Order.Id,
                fixture.Assignment.Id, fixture.Assignment.ProductUnitId, "Kit", FaultSeverity.Medium, "Test", Now);
            if (status != FaultStatus.Open) ticket.ChangeStatus(status, Actor, Now, "Test aşaması");
            if (status == FaultStatus.CustomerShipmentInTransit)
            {
                ticket.CreateKargonomiShipment(FaultKargonomiShipmentDirection.ToWorkshop, "Depo", "5550001122", "Depo adresi", Now)
                    .MarkCreated(401, "shipped", "Kargoda", "PICKUP", Now, "HepsiJet");
                ticket.CreateKargonomiShipment(FaultKargonomiShipmentDirection.ToCustomer, "Veli", "5550001122", "Veli adresi", Now)
                    .MarkCreated(402, "delivered", "Teslim edildi", "OUTBOUND", Now, "Aras Kargo");
            }
            await fixture.Repository.AddFaultTicketAsync(ticket, Token);
        }
        var portal = new CustomerPortalService(fixture.Repository, fixture.Operations,
            new KargonomiShippingService(fixture.Repository, new FakeKargonomiClient(), new FixedTimeProvider()), fixture.Overview);
        var rows = await portal.GetFaultsPageAsync(fixture.Customer.Id, Token);
        var admin = await fixture.Overview.GetDashboardAsync(fixture.Customer.Id, Token);
        Assert.Equal(admin.OpenFaultCount, rows.Faults.Count(item => item.IsOpen));
        Assert.Equal(admin.FaultsCompleted, rows.Faults.Count(item => item.Stage == "completed"));
        Assert.False(rows.Faults.Single(item => item.Status == FaultStatus.Rejected).IsOpen);
        var fault = rows.Faults.Single(item => item.Status == FaultStatus.CustomerShipmentInTransit);
        Assert.Equal(2, fault.Shipments!.Count);
        Assert.Equal("PICKUP", fault.Shipments.Single(item => item.Direction == 1).TrackingNumber);
        Assert.Equal("OUTBOUND", fault.Shipments.Single(item => item.Direction == 2).TrackingNumber);
        await Assert.ThrowsAsync<KitRental.Core.Application.Common.ResourceNotFoundException>(() =>
            portal.GetFaultAsync(Guid.NewGuid(), fault.Id, Token));
        var periods = await portal.GetRentalPeriodsPageAsync(fixture.Customer.Id, Token, "active");
        Assert.Equal(admin.ActiveKitCount, Assert.Single(periods.RentalCohorts).Progress!.ActiveKitCount);
        Assert.Empty((await portal.GetRentalPeriodsPageAsync(fixture.Customer.Id, Token, "shipment-failed")).RentalCohorts);
    }

    [Fact]
    public async Task CustomerOrderProgressIncludesOrdersWithoutACohortAndExcludesOtherCustomers()
    {
        var fixture = await SeedAsync(new DateOnly(2026, 10, 1));
        var standalone = RentalOrder.Create(Guid.NewGuid(), "DIRECT-1", fixture.Customer.Id,
            new RentalPeriod(new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1)),
            new AddressSnapshot("Alıcı", "5550001122", "Adres", "34000"), Now);
        standalone.AddLine(fixture.Order.Lines.First().ProductModelId, 1);
        standalone.Submit(Actor, Now);
        await fixture.Repository.AddOrderAsync(standalone, Token);
        var other = RentalOrder.Create(Guid.NewGuid(), "OTHER-1", Guid.NewGuid(),
            new RentalPeriod(new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1)),
            new AddressSnapshot("Başka alıcı", "5550001122", "Adres", "34000"), Now);
        await fixture.Repository.AddOrderAsync(other, Token);
        var portal = new CustomerPortalService(fixture.Repository, fixture.Operations,
            new KargonomiShippingService(fixture.Repository, new FakeKargonomiClient(), new FixedTimeProvider()), fixture.Overview);
        var periods = await portal.GetRentalPeriodsPageAsync(fixture.Customer.Id, Token, "approval");
        Assert.Empty(periods.RentalCohorts);
        Assert.Equal(standalone.Id, Assert.Single(periods.StandaloneOrders!).Id);
        var dashboard = await portal.GetDashboardAsync(fixture.Customer.Id, Token);
        Assert.Equal(2, dashboard.Operations!.TotalOrders);
        Assert.DoesNotContain(dashboard.Operations.PriorityOrders, item => item.Id == other.Id);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
    // UTC is still September 24; operational dates must already be September 25 in Turkey.
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 22, 30, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public async Task ReceivedReturnWithExternalShipmentIsOnlyCountedAsCompleted()
    {
        var fixture = await SeedAsync(new DateOnly(2026, 9, 24));
        var older = NewReturn(fixture, Now.AddDays(-2));
        older.LinkExternalShipment(123, "Aras", "TRACK", null, "shipped", "Kargoda", Now);
        await fixture.Repository.AddKitReturnRequestAsync(older, Token);
        var latest = NewReturn(fixture, Now.AddDays(-1));
        latest.LinkExternalShipment(124, "Aras", "TRACK-2", null, "delivered", "Teslim edildi", Now);
        latest.Receive(Now);
        await fixture.Repository.AddKitReturnRequestAsync(latest, Token);

        var dashboard = await fixture.Overview.GetDashboardAsync(fixture.Customer.Id, Token);
        var returns = await fixture.Operations.GetReturnsTableAsync(Token, fixture.Customer.Id);

        Assert.Equal(1, dashboard.ReturnCompletedKitCount);
        Assert.Equal(0, dashboard.ReturnInTransitKitCount);
        Assert.Equal(0, dashboard.ReturnPendingKitCount);
        Assert.Equal(0, dashboard.ActiveKitCount);
        Assert.Equal(0, dashboard.OverdueOrders);
        Assert.Equal("completed", Assert.Single(returns).ReturnStateKey);
        Assert.Empty(await fixture.Operations.GetReturnsTableAsync(Token, fixture.Customer.Id, state: "in-transit"));
    }

    [Fact]
    public async Task FulfilledOrderStillRequiresReturnWhenActiveRentalExpires()
    {
        var fixture = await SeedAsync(new DateOnly(2026, 9, 24));
        fixture.Order.CompleteFulfillment(Actor, Now);
        var dashboard = await fixture.Overview.GetDashboardAsync(fixture.Customer.Id, Token);
        var orders = await fixture.Overview.GetOrdersAsync(new OperationsOrderQuery(
            CustomerId: fixture.Customer.Id, Focus: "overdue"), Token);
        var returns = await fixture.Operations.GetReturnsTableAsync(Token, fixture.Customer.Id, state: "missing-form");
        Assert.Equal(1, dashboard.OverdueOrders);
        Assert.Equal(1, dashboard.ActiveKitCount);
        Assert.Equal(1, dashboard.MissingReturnFormCount);
        Assert.Equal(1, dashboard.ReturnPendingKitCount);
        Assert.Equal(fixture.Order.Id, Assert.Single(orders.Items).Id);
        Assert.Equal(fixture.Assignment.Id, Assert.Single(returns).AssignmentId);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(7, true)]
    [InlineData(8, false)]
    [InlineData(-1, false)]
    public async Task EndingSoonUsesTurkeyDateAndIncludesTheSeventhDay(int days, bool expected)
    {
        var fixture = await SeedAsync(new DateOnly(2026, 9, 25).AddDays(days));
        var page = await fixture.Overview.GetOrdersAsync(new OperationsOrderQuery(), Token);
        Assert.Equal(expected, Assert.Single(page.Items).IsEndingSoon);
    }

    [Fact]
    public async Task CustomerScopeAndFocusAreAppliedBeforePaging()
    {
        var fixture = await SeedAsync(new DateOnly(2026, 9, 24));
        var another = Customer.Create(Guid.NewGuid(), "Başka müşteri", "other@example.test");
        await fixture.Repository.AddCustomerAsync(another, Token);
        for (var i = 0; i < 12; i++)
        {
            var order = RentalOrder.Create(Guid.NewGuid(), $"OTHER-{i}", another.Id,
                new RentalPeriod(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 24)),
                new AddressSnapshot("Alıcı", "5550001122", "Adres", "34000"), Now.AddMinutes(i));
            order.Submit(Actor, Now);
            await fixture.Repository.AddOrderAsync(order, Token);
        }

        var page = await fixture.Overview.GetOrdersAsync(new OperationsOrderQuery(CustomerId: another.Id,
            Focus: "approval", Page: 2, PageSize: 10), Token);
        Assert.Equal(12, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.All(page.Items, item => Assert.Equal(another.Id, item.CustomerId));
        var own = await fixture.Overview.GetDashboardAsync(fixture.Customer.Id, Token);
        Assert.Equal(1, own.TotalOrders);
        Assert.Equal(0, own.PendingApprovalOrders);
        var outside = await fixture.Overview.GetOrdersAsync(new OperationsOrderQuery(
            CustomerId: fixture.Customer.Id, Query: "OTHER"), Token);
        Assert.Empty(outside.Items);
    }

    [Fact]
    public async Task FailedShipmentIsRetryableWhileReadyShipmentIsNotInTransit()
    {
        var fixture = await SeedAsync(new DateOnly(2026, 10, 1));
        var shipment = KargonomiShipment.Create(Guid.NewGuid(), fixture.Order.Id, fixture.Student.Id, Now);
        shipment.MarkFailed("Adres kontrol edilmeli", Now);
        await fixture.Repository.AddKargonomiShipmentAsync(shipment, Token);
        var failed = await fixture.Overview.GetDashboardAsync(fixture.Customer.Id, Token);
        Assert.Equal(1, failed.ShipmentsFailed);
        Assert.Equal(1, failed.StudentsAwaitingShipment);
        Assert.Equal(0, failed.ShipmentsInTransit);
        var linked = await fixture.Overview.GetOrdersAsync(new OperationsOrderQuery(Focus: "shipment-failed"), Token);
        Assert.Single(linked.Items);

        shipment.MarkCreated(123, "ready", "Hazır", "TRACK", Now);
        var ready = await fixture.Overview.GetDashboardAsync(fixture.Customer.Id, Token);
        Assert.Equal(0, ready.ShipmentsFailed);
        Assert.Equal(0, ready.StudentsAwaitingShipment);
        Assert.Equal(1, ready.ShipmentsReady);
        Assert.Equal(0, ready.ShipmentsInTransit);
    }

    [Fact]
    public async Task FaultGroupsMatchLinkedListsAndKeepCustomerAndOrderScope()
    {
        var fixture = await SeedAsync(new DateOnly(2026, 10, 1));
        foreach (var status in new[] { FaultStatus.Open, FaultStatus.RemoteResolved, FaultStatus.Closed,
                     FaultStatus.Rejected, FaultStatus.CustomerShipmentInTransit, FaultStatus.WorkshopReceived })
        {
            var ticket = FaultTicket.Open(Guid.NewGuid(), $"FAULT-{status}", fixture.Customer.Id,
                fixture.Order.Id, fixture.Assignment.Id, fixture.Assignment.ProductUnitId,
                "Kit", FaultSeverity.Medium, "Test arızası", Now);
            if (status != FaultStatus.Open) ticket.ChangeStatus(status, Actor, Now, "Test durumu");
            await fixture.Repository.AddFaultTicketAsync(ticket, Token);
        }
        var dashboard = await fixture.Overview.GetDashboardAsync(fixture.Customer.Id, Token);
        var open = await fixture.Operations.GetFaultPageAsync(new FaultPageQuery(null, null, null, null, null,
            CustomerId: fixture.Customer.Id, OrderId: fixture.Order.Id, Stage: "open"), Token);
        var completed = await fixture.Operations.GetFaultPageAsync(new FaultPageQuery(null, null, null, null, null,
            CustomerId: fixture.Customer.Id, Stage: "completed"), Token);
        Assert.Equal(3, dashboard.OpenFaultCount);
        Assert.Equal(dashboard.OpenFaultCount, open.TotalCount);
        Assert.Equal(2, dashboard.FaultsCompleted);
        Assert.Equal(dashboard.FaultsCompleted, completed.TotalCount);
        var outside = await fixture.Operations.GetFaultPageAsync(new FaultPageQuery(null, null, null, null, null,
            CustomerId: Guid.NewGuid(), OrderId: fixture.Order.Id), Token);
        Assert.Empty(outside.Items);
    }

    [Fact]
    public async Task DraftOrdersDoNotGenerateAddressOrShippingWork()
    {
        var fixture = await SeedAsync(new DateOnly(2026, 10, 1), approve: false);
        var dashboard = await fixture.Overview.GetDashboardAsync(fixture.Customer.Id, Token);
        Assert.Equal(1, dashboard.TotalOrders);
        Assert.Equal(0, dashboard.StudentsAwaitingShipment);
        Assert.Equal(0, dashboard.StudentsAwaitingAddress);
        Assert.Empty(await fixture.Operations.GetReturnsTableAsync(Token, orderId: Guid.NewGuid()));
    }

    private static KitReturnRequest NewReturn(Fixture fixture, DateTimeOffset createdAt) =>
        KitReturnRequest.Create(Guid.NewGuid(), fixture.Customer.Id, createdAt, Actor,
            [new KitReturnItem(Guid.NewGuid(), fixture.Assignment.Id, fixture.Assignment.ProductUnitId, fixture.Order.Id)]);

    private static async Task<Fixture> SeedAsync(DateOnly end, bool approve = true)
    {
        var repository = new InMemoryCoreRepository();
        var clock = new FixedTimeProvider();
        var customer = Customer.Create(Guid.NewGuid(), "Tacev", "tacev@example.test");
        var model = ProductModel.Create(Guid.NewGuid(), "Robotik kit", "KIT");
        await repository.AddCustomerAsync(customer, Token);
        await repository.AddProductModelAsync(model, Token);
        var order = RentalOrder.Create(Guid.NewGuid(), "ORDER-1", customer.Id,
            new RentalPeriod(new DateOnly(2026, 9, 1), end),
            new AddressSnapshot("Alıcı", "5550001122", "Adres", "34000"), Now);
        var line = order.AddLine(model.Id, 1);
        if (approve) { order.Submit(Actor, Now); order.Approve(Actor, Now); }
        await repository.AddOrderAsync(order, Token);
        var unit = ProductUnit.Create(Guid.NewGuid(), model.Id, "SERIAL", "QR", Actor, Now);
        await repository.AddProductUnitAsync(unit, Token);
        var assignment = RentalAssignment.Create(Guid.NewGuid(), line.Id, customer.Id, unit.Id, Now, Actor);
        Assert.True(await repository.TryCreateReservationAsync(unit, assignment, Actor, Now, Token));
        if (approve) assignment.Activate();
        var cohort = RentalCohort.Create(Guid.NewGuid(), customer.Id, "Güz dönemi", new DateOnly(2026, 9, 1), end, Now);
        var student = cohort.AddStudent("Öğrenci", "5550001122", "İstanbul / Kadıköy - Adres", model.Id);
        cohort.LinkStudentToKit(student.Id, order.Id, assignment.Id, unit.Id);
        await repository.AddRentalCohortAsync(cohort, Token);
        return new Fixture(repository, customer, order, student, assignment,
            new OperationsOverviewService(repository, clock),
            new OperationsService(repository, clock, new ProductUnitStockConsumptionPlanner(repository)));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed record Fixture(InMemoryCoreRepository Repository, Customer Customer, RentalOrder Order,
        RentalCohortStudent Student, RentalAssignment Assignment, OperationsOverviewService Overview,
        OperationsService Operations);
}
