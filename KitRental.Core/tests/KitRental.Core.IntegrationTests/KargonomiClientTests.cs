using System.Net;
using System.Text;
using System.Text.Json;
using KitRental.Core.Api.Services;
using KitRental.Core.Application.Common;
using KitRental.Core.Application.Kargonomi;
using Microsoft.Extensions.Configuration;

namespace KitRental.Core.IntegrationTests;

public sealed class KargonomiClientTests
{
    [Fact]
    public async Task LocationResolutionUsesStoredRegionIdsAndCachesProviderLists()
    {
        var handler = new LocationHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var client = new KargonomiClient(httpClient, LocationConfiguration());

        var location = await client.ResolveLocationAsync(34, 1, TestContext.Current.CancellationToken);
        var repeated = await client.ResolveLocationAsync(34, 1, TestContext.Current.CancellationToken);

        Assert.Equal((34, 1), location);
        Assert.Equal(location, repeated);
        Assert.Equal(new[] { "/api/v1/states/1", "/api/v1/cities/34" }, handler.RequestPaths);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(34, null)]
    [InlineData(null, 1)]
    [InlineData(0, 1)]
    [InlineData(34, -1)]
    public async Task MissingRegionIdsFailBeforeProviderLookup(int? cityId, int? districtId)
    {
        var handler = new LocationHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var client = new KargonomiClient(httpClient, LocationConfiguration());

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            client.ResolveLocationAsync(cityId, districtId, TestContext.Current.CancellationToken));

        Assert.Equal("kargonomi.address_region_missing", error.Code);
        Assert.Empty(handler.RequestPaths);
    }

    [Fact]
    public async Task DistrictMustBelongToSelectedProvince()
    {
        var handler = new LocationHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var client = new KargonomiClient(httpClient, LocationConfiguration());

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            client.ResolveLocationAsync(34, 999, TestContext.Current.CancellationToken));

        Assert.Equal("kargonomi.city_not_found", error.Code);
        Assert.Equal(2, handler.RequestPaths.Count);
    }

    private static IConfiguration LocationConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kargonomi:BaseUrl"] = "https://kargonomi.test/api/v1",
            ["Kargonomi:ApiToken"] = "test-token"
        }).Build();

    private sealed class LocationHttpMessageHandler : HttpMessageHandler
    {
        public List<string> RequestPaths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            RequestPaths.Add(path);
            var body = path switch
            {
                "/api/v1/states/1" => "[{\"id\":34,\"name\":\"İstanbul\"}]",
                "/api/v1/cities/34" => "{\"data\":[{\"id\":1,\"name\":\"Kadıköy\"}]}",
                _ => throw new InvalidOperationException($"Unexpected provider path: {path}")
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    [Fact]
    public async Task ReturnShipmentUsesPickupAddressAndConfiguredWorkshopWithoutWarehouseOverride()
    {
        var handler = new CapturingHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kargonomi:BaseUrl"] = "https://kargonomi.test/api/v1",
            ["Kargonomi:ApiToken"] = "test-token",
            ["Kargonomi:WarehouseId"] = "136751",
            ["Kargonomi:SenderName"] = "Robotik Bilim Atölyesi",
            ["Kargonomi:SenderEmail"] = "test@example.com",
            ["Kargonomi:SenderPhone"] = "05536589698",
            ["Kargonomi:SenderTaxNumber"] = "55981171232",
            ["Kargonomi:SenderAddress"] = "Atölye adresi",
            ["Kargonomi:SenderStateId"] = "34",
            ["Kargonomi:SenderCityId"] = "332"
        }).Build();
        var client = new KargonomiClient(httpClient, configuration);

        await client.CreateReturnShipmentAsync(new KargonomiReturnShipmentRequest(
            "Veli Adı", "05074130302", "Seçilen arıza adresi", 6, 104,
            "Arızalı kit", "FAULT-01", 1), CancellationToken.None);

        using var payload = JsonDocument.Parse(handler.RequestBody!);
        var shipment = payload.RootElement.GetProperty("shipment");
        Assert.False(shipment.TryGetProperty("warehouse_id", out _));
        Assert.Equal("Veli Adı", shipment.GetProperty("sender_name").GetString());
        Assert.Equal("55981171232", shipment.GetProperty("sender_tax_number").GetString());
        Assert.Equal("Seçilen arıza adresi", shipment.GetProperty("sender_address").GetString());
        Assert.Equal(6, shipment.GetProperty("sender_state_id").GetInt32());
        Assert.Equal(104, shipment.GetProperty("sender_city_id").GetInt32());
        Assert.Equal("Robotik Bilim Atölyesi", shipment.GetProperty("buyer_name").GetString());
        Assert.Equal("Atölye adresi", shipment.GetProperty("buyer_address").GetString());
        Assert.Equal(34, shipment.GetProperty("buyer_state_id").GetInt32());
        Assert.Equal(332, shipment.GetProperty("buyer_city_id").GetInt32());
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"shipment\":{\"id\":123}}", Encoding.UTF8, "application/json")
            };
        }
    }
}
