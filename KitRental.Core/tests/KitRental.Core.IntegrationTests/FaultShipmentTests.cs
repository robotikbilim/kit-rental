using KitRental.Core.Application.Common;
using KitRental.Core.Application.Inventory;
using KitRental.Core.Application.Kargonomi;
using KitRental.Core.Application.Operations;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Support;
using KitRental.Core.Infrastructure.Persistence;
using KitRental.SharedKernel;

namespace KitRental.Core.IntegrationTests;

public sealed class FaultShipmentTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static readonly Guid Actor = Guid.NewGuid();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BothLegsCanStartInEitherOrderBeforeCollection(bool outboundFirst)
    {
        var fixture = await SeedAsync();
        var directions = outboundFirst
            ? new[] { FaultKargonomiShipmentDirection.ToCustomer, FaultKargonomiShipmentDirection.ToWorkshop }
            : new[] { FaultKargonomiShipmentDirection.ToWorkshop, FaultKargonomiShipmentDirection.ToCustomer };
        foreach (var direction in directions)
            await fixture.Shipping.StartForFaultAsync(fixture.Ticket.Id, direction, Token);

        var outbound = Assert.Single(fixture.Client.OutboundRequests);
        var pickup = Assert.Single(fixture.Client.ReturnRequests);
        Assert.Equal(fixture.Ticket.ReporterAddress, outbound.BuyerAddress);
        Assert.Equal(fixture.Ticket.ReporterAddress, pickup.SenderAddress);
        Assert.Equal(fixture.Ticket.ReporterPhone, pickup.SenderPhone);
        Assert.Equal(fixture.Ticket.ReporterName, outbound.BuyerName);
        Assert.Equal(FaultStatus.CustomerShipmentInTransit, fixture.Ticket.Status);
        Assert.Equal(2, fixture.Ticket.KargonomiShipments.Count);
        var returning = fixture.Ticket.KargonomiShipments.Single(item => item.Direction == FaultKargonomiShipmentDirection.ToWorkshop);
        Assert.Equal(fixture.Client.GetReturnDestination().Address, returning.RecipientAddress);
        Assert.Equal("HepsiJet", returning.Carrier);
        Assert.Equal("Aras Kargo", fixture.Ticket.KargonomiShipments.Single(item => item.Direction == FaultKargonomiShipmentDirection.ToCustomer).Carrier);

        var label = await fixture.Operations.GetFaultKitLabelAsync(fixture.Ticket.Id, Token);
        Assert.Equal(fixture.Unit.Id, label.Id);
        Assert.Equal("KIT-00001", label.SerialNumber);
        Assert.Equal("ORIGINAL-QR", label.QrCode);
        var page = await fixture.Operations.GetFaultPageAsync(new FaultPageQuery(null, null, null, null, null), Token);
        var row = Assert.Single(page.Items);
        Assert.Equal(label.SerialNumber, row.SerialNumber);
        Assert.Equal(2, row.Shipments!.Count);

        // Repeated clicks reuse each independent leg.
        foreach (var direction in directions)
            await fixture.Shipping.StartForFaultAsync(fixture.Ticket.Id, direction, Token);
        Assert.Single(fixture.Client.OutboundRequests);
        Assert.Single(fixture.Client.ReturnRequests);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeliveryOfOneLegDoesNotCloseOrOverwriteTheOther(bool outboundDeliveredFirst)
    {
        var fixture = await SeedAsync();
        var outbound = await fixture.Shipping.StartForFaultAsync(fixture.Ticket.Id, FaultKargonomiShipmentDirection.ToCustomer, Token);
        // Outbound delivery may arrive before the courier is even requested.
        if (outboundDeliveredFirst)
            await fixture.Shipping.ApplyFaultWebhookAsync(outbound.ExternalShipmentId!.Value, "delivered", "Teslim edildi", "OUT", null, Token);
        Assert.NotEqual(FaultStatus.Closed, fixture.Ticket.Status);
        var pickup = await fixture.Shipping.StartForFaultAsync(fixture.Ticket.Id, FaultKargonomiShipmentDirection.ToWorkshop, Token);
        await fixture.Shipping.ApplyFaultWebhookAsync(pickup.ExternalShipmentId!.Value, "delivered", "Depoya teslim edildi", "IN", null, Token);
        if (!outboundDeliveredFirst)
        {
            Assert.NotEqual(FaultStatus.Closed, fixture.Ticket.Status);
            await fixture.Shipping.ApplyFaultWebhookAsync(outbound.ExternalShipmentId!.Value, "delivered", "Teslim edildi", "OUT", null, Token);
        }
        Assert.Equal(FaultStatus.Closed, fixture.Ticket.Status);
        Assert.All(fixture.Ticket.KargonomiShipments, shipment => Assert.Equal(KargonomiShipmentState.Delivered, shipment.State));
        Assert.Equal("IN", fixture.Ticket.KargonomiShipments.Single(item => item.Id == pickup.Id).TrackingNumber);
        Assert.Equal("OUT", fixture.Ticket.KargonomiShipments.Single(item => item.Id == outbound.Id).TrackingNumber);
        var historyCount = fixture.Ticket.History.Count;
        await fixture.Shipping.ApplyFaultWebhookAsync(outbound.ExternalShipmentId.Value, "delivered", "Teslim edildi", "OUT", null, Token);
        Assert.Equal(historyCount, fixture.Ticket.History.Count);
    }

    [Theory]
    [InlineData(FaultKargonomiShipmentDirection.ToWorkshop)]
    [InlineData(FaultKargonomiShipmentDirection.ToCustomer)]
    public async Task ConfirmationRetryKeepsProviderId(FaultKargonomiShipmentDirection direction)
    {
        var fixture = await SeedAsync();
        fixture.Client.FailNextConfirmation = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Shipping.StartForFaultAsync(fixture.Ticket.Id, direction, Token));
        var failed = Assert.Single(fixture.Ticket.KargonomiShipments);
        var externalId = failed.ExternalShipmentId;
        Assert.NotNull(externalId);
        Assert.Equal(KargonomiShipmentState.Failed, failed.State);
        var retried = await fixture.Shipping.StartForFaultAsync(fixture.Ticket.Id, direction, Token);
        Assert.Equal(externalId, retried.ExternalShipmentId);
        Assert.Single(fixture.Ticket.KargonomiShipments);
        Assert.Equal(1, fixture.Client.OutboundRequests.Count + fixture.Client.ReturnRequests.Count);
    }

    [Theory]
    [InlineData(FaultStatus.Open)]
    [InlineData(FaultStatus.Investigating)]
    [InlineData(FaultStatus.RemoteResolved)]
    [InlineData(FaultStatus.Rejected)]
    [InlineData(FaultStatus.Closed)]
    public async Task IneligibleFaultCannotCreateEitherShipment(FaultStatus status)
    {
        var fixture = await SeedAsync();
        fixture.Ticket.ChangeStatus(status, Actor, DateTimeOffset.UtcNow, "Test aşaması");
        foreach (var direction in new[] { FaultKargonomiShipmentDirection.ToWorkshop, FaultKargonomiShipmentDirection.ToCustomer })
            await Assert.ThrowsAsync<DomainException>(() => fixture.Shipping.StartForFaultAsync(fixture.Ticket.Id, direction, Token));
        Assert.Empty(fixture.Client.ReturnRequests);
        Assert.Empty(fixture.Client.OutboundRequests);
    }

    [Fact]
    public async Task LabelLookupRejectsShipmentFromAnotherFault()
    {
        var fixture = await SeedAsync();
        var shipment = await fixture.Shipping.StartForFaultAsync(fixture.Ticket.Id, FaultKargonomiShipmentDirection.ToCustomer, Token);
        Assert.Equal($"BARCODE-{shipment.ExternalShipmentId}", await fixture.Shipping.GetFaultBarcodeAsync(fixture.Ticket.Id, shipment.Id, Token));
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => fixture.Shipping.GetFaultBarcodeAsync(fixture.Ticket.Id, Guid.NewGuid(), Token));
    }

    private static async Task<Fixture> SeedAsync()
    {
        var repository = new InMemoryCoreRepository();
        var model = ProductModel.Create(Guid.NewGuid(), "Robotik Kit", "KIT");
        var unit = ProductUnit.Create(Guid.NewGuid(), model.Id, "KIT-00001", "ORIGINAL-QR", Actor, DateTimeOffset.UtcNow);
        await repository.AddProductModelAsync(model, Token);
        await repository.AddProductUnitAsync(unit, Token);
        var ticket = FaultTicket.Open(Guid.NewGuid(), "FAULT-01", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), unit.Id,
            "Arıza", FaultSeverity.Medium, "Çalışmıyor", DateTimeOffset.UtcNow,
            "Veli", "5550001122", "Ankara / Çankaya - Veli adresi");
        ticket.MarkInvestigating(Actor, DateTimeOffset.UtcNow, "İncelendi");
        ticket.Accept(Actor, DateTimeOffset.UtcNow, "Kabul edildi");
        await repository.AddFaultTicketAsync(ticket, Token);
        var client = new FakeKargonomiClient();
        return new Fixture(ticket, unit, client, new KargonomiShippingService(repository, client, TimeProvider.System),
            new OperationsService(repository, TimeProvider.System, new ProductUnitStockConsumptionPlanner(repository)));
    }

    private sealed record Fixture(FaultTicket Ticket, ProductUnit Unit, FakeKargonomiClient Client,
        KargonomiShippingService Shipping, OperationsService Operations);
}
