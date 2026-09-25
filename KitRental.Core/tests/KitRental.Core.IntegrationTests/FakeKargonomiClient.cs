using KitRental.Core.Application.Kargonomi;

namespace KitRental.Core.IntegrationTests;

internal sealed class FakeKargonomiClient : IKargonomiClient
{
    private int nextShipmentId = 1000;

    public Task<KargonomiShipmentSnapshot> CreateShipmentAsync(KargonomiCreateShipmentRequest request,
        CancellationToken cancellationToken) => Task.FromResult(CreateSnapshot(request.PackageBarcode));

    public Task<KargonomiShipmentSnapshot> CreateReturnShipmentAsync(KargonomiReturnShipmentRequest request,
        CancellationToken cancellationToken) => Task.FromResult(CreateSnapshot($"RETURN-{Interlocked.Increment(ref nextShipmentId)}"));

    public Task<IReadOnlyCollection<KargonomiCarrierQuote>> GetPriceQuotesAsync(int shipmentId,
        CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<KargonomiCarrierQuote>>(
        [new KargonomiCarrierQuote(6, "HepsiJet", "hepsijet", "100")]);

    public Task<KargonomiShipmentSnapshot> ConfirmShippingPriceAsync(int shipmentId, int providerId,
        CancellationToken cancellationToken) => Task.FromResult(new KargonomiShipmentSnapshot(
        shipmentId, "confirmed", "Onaylandı", "HepsiJet", $"RETURN-{shipmentId}", $"BARCODE-{shipmentId}", DateTimeOffset.UtcNow));

    public Task<KargonomiShipmentSnapshot> GetShipmentAsync(int shipmentId, CancellationToken cancellationToken) =>
        Task.FromResult(CreateSnapshot($"SHIPMENT-{shipmentId}"));

    public Task<IReadOnlyCollection<KargonomiShipmentListSnapshot>> GetShipmentsAsync(
        CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<KargonomiShipmentListSnapshot>>([]);

    public Task<string> GetBarcodeAsync(int shipmentId, CancellationToken cancellationToken) =>
        Task.FromResult($"BARCODE-{shipmentId}");

    public Task<(int StateId, int CityId)> ResolveLocationAsync(string address,
        CancellationToken cancellationToken) => Task.FromResult((34, 1));

    private static KargonomiShipmentSnapshot CreateSnapshot(string trackingNumber) =>
        new(Interlocked.Increment(ref shipmentId), "created", "Hazır", "HepsiJet", trackingNumber, null, DateTimeOffset.UtcNow);

    private static int shipmentId = 2000;
}
