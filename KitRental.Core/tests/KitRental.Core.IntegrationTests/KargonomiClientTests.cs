using System.Net;
using System.Text;
using System.Text.Json;
using KitRental.Core.Api.Services;
using KitRental.Core.Application.Kargonomi;
using Microsoft.Extensions.Configuration;

namespace KitRental.Core.IntegrationTests;

public sealed class KargonomiClientTests
{
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
            ["Kargonomi:SenderAddress"] = "İstanbul / Esenler - Atölye adresi",
            ["Kargonomi:SenderStateId"] = "34",
            ["Kargonomi:SenderCityId"] = "332"
        }).Build();
        var client = new KargonomiClient(httpClient, configuration);

        await client.CreateReturnShipmentAsync(new KargonomiReturnShipmentRequest(
            "Veli Adı", "05074130302", "Ankara / Çankaya - Seçilen arıza adresi", 6, 104,
            "Arızalı kit", "FAULT-01", 1), CancellationToken.None);

        using var payload = JsonDocument.Parse(handler.RequestBody!);
        var shipment = payload.RootElement.GetProperty("shipment");
        Assert.False(shipment.TryGetProperty("warehouse_id", out _));
        Assert.Equal("Veli Adı", shipment.GetProperty("sender_name").GetString());
        Assert.Equal("55981171232", shipment.GetProperty("sender_tax_number").GetString());
        Assert.Equal("Ankara / Çankaya - Seçilen arıza adresi", shipment.GetProperty("sender_address").GetString());
        Assert.Equal(6, shipment.GetProperty("sender_state_id").GetInt32());
        Assert.Equal(104, shipment.GetProperty("sender_city_id").GetInt32());
        Assert.Equal("Robotik Bilim Atölyesi", shipment.GetProperty("buyer_name").GetString());
        Assert.Equal("İstanbul / Esenler - Atölye adresi", shipment.GetProperty("buyer_address").GetString());
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
