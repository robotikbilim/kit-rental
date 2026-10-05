using KitRental.Core.Application.Common;
using KitRental.Core.Application.Kargonomi;

namespace KitRental.Core.IntegrationTests;

internal sealed class FakeKargonomiClient : IKargonomiClient
{
    private int nextShipmentId = 1000;
    public List<KargonomiCreateShipmentRequest> OutboundRequests { get; } = [];
    public List<KargonomiReturnShipmentRequest> ReturnRequests { get; } = [];
    public List<(int? CityId, int? DistrictId)> AddressResolutionRequests { get; } = [];
    public bool FailNextLocationResolution { get; set; }
    public bool FailNextConfirmation { get; set; }
    public KargonomiReturnDestination GetReturnDestination() => new("Robotik Bilim", "5551112233", "Robotik Bilim deposu",
        34, 1, "İstanbul", "Kadıköy");

    public Task<KargonomiShipmentSnapshot> CreateShipmentAsync(KargonomiCreateShipmentRequest request,
        CancellationToken cancellationToken)
    {
        OutboundRequests.Add(request);
        return Task.FromResult(CreateSnapshot(request.PackageBarcode));
    }

    public Task<KargonomiShipmentSnapshot> CreateReturnShipmentAsync(KargonomiReturnShipmentRequest request,
        CancellationToken cancellationToken)
    {
        ReturnRequests.Add(request);
        return Task.FromResult(CreateSnapshot($"RETURN-{Interlocked.Increment(ref nextShipmentId)}"));
    }

    public Task<IReadOnlyCollection<KargonomiCarrierQuote>> GetPriceQuotesAsync(int shipmentId,
        CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<KargonomiCarrierQuote>>(
        [new KargonomiCarrierQuote(6, "HepsiJet", "hepsijet", "100"), new KargonomiCarrierQuote(7, "Aras Kargo", "aras", "100")]);

    public Task<KargonomiShipmentSnapshot> ConfirmShippingPriceAsync(int shipmentId, int providerId,
        CancellationToken cancellationToken)
    {
        if (FailNextConfirmation)
        {
            FailNextConfirmation = false;
            throw new HttpRequestException("Geçici kargo onay hatası");
        }
        return Task.FromResult(new KargonomiShipmentSnapshot(
            shipmentId, "confirmed", "Onaylandı", providerId == 6 ? "HepsiJet" : "Aras Kargo",
            $"RETURN-{shipmentId}", $"BARCODE-{shipmentId}", DateTimeOffset.UtcNow));
    }

    public Task<KargonomiShipmentSnapshot> GetShipmentAsync(int shipmentId, CancellationToken cancellationToken) =>
        Task.FromResult(CreateSnapshot($"SHIPMENT-{shipmentId}"));

    public Task<IReadOnlyCollection<KargonomiShipmentListSnapshot>> GetShipmentsAsync(
        CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<KargonomiShipmentListSnapshot>>([]);

    public Task<string> GetBarcodeAsync(int shipmentId, CancellationToken cancellationToken) =>
        Task.FromResult($"BARCODE-{shipmentId}");

    public Task<IReadOnlyCollection<KargonomiRegionResponse>> GetStatesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<KargonomiRegionResponse>>([
            new(34, "İstanbul"), new(6, "Ankara"), new(35, "İzmir")]);

    public Task<IReadOnlyCollection<KargonomiRegionResponse>> GetCitiesAsync(int stateId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<KargonomiRegionResponse>>(stateId switch
        {
            34 => [new(1, "Kadıköy"), new(332, "Esenler")],
            6 => [new(104, "Çankaya")],
            35 => [new(351, "Bornova")],
            _ => []
        });

    public Task<(int StateId, int CityId)> ResolveLocationAsync(int? cityId, int? districtId,
        CancellationToken cancellationToken)
    {
        AddressResolutionRequests.Add((cityId, districtId));
        if (FailNextLocationResolution || cityId is null or <= 0 || districtId is null or <= 0)
        {
            FailNextLocationResolution = false;
            throw new ConflictException("kargonomi.address_region_missing", "Adres şehir / ilçe formatında olmalıdır.");
        }
        return Task.FromResult((cityId.Value, districtId.Value));
    }

    private static KargonomiShipmentSnapshot CreateSnapshot(string trackingNumber) =>
        new(Interlocked.Increment(ref shipmentId), "created", "Hazır", "HepsiJet", trackingNumber, null, DateTimeOffset.UtcNow);

    private static int shipmentId = 2000;
}
