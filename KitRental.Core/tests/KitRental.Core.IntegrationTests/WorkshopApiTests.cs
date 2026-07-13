using System.Net.Http.Headers;
using System.Net.Http.Json;
using KitRental.Core.Application.Inventory;
using KitRental.Core.Application.Workshop;
using KitRental.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KitRental.Core.IntegrationTests;

public sealed class WorkshopApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public WorkshopApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing")).CreateClient();
        var tokens = new TokenService(new TokenOptions(
            "KitRental.Identity", "KitRental", "development-only-secret-change-before-production-2026", TimeSpan.FromHours(8)));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            tokens.Create(new TokenUser(Guid.NewGuid(), "warehouse@test.local", "SystemAdmin", null), DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task CreateKit_CreatesCatalogRecordAndRecipeTogether()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var component = await PostAsync<ComponentResponse>("/api/components",
            new CreateComponentRequest("Kit Test Komponenti", $"CMP-{Guid.NewGuid():N}", "adet", 2), cancellationToken);

        var kit = await PostAsync<KitCatalogResponse>("/api/kits",
            new CreateKitRequest("Test Eğitim Kiti", $"KIT-{Guid.NewGuid():N}", "Reçeteli test kiti",
                "/images/catalog/kit.svg", 1, [new BillOfMaterialsLineRequest(component.Id, 3)]), cancellationToken);
        var catalog = await _client.GetFromJsonAsync<ProductModelResponse[]>("/api/product-models", cancellationToken);
        var bom = await _client.GetFromJsonAsync<BillOfMaterialsResponse>($"/api/product-models/{kit.Id}/bom", cancellationToken);

        Assert.Contains(catalog!, item => item.Id == kit.Id && item.Description == "Reçeteli test kiti");
        Assert.Equal(3, bom!.Lines.Single().Quantity);
    }

    [Fact]
    public async Task ComponentStockAndBom_CalculateBuildableKitAndTrackShelves()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var product = await PostAsync<ProductModelResponse>("/api/product-models",
            new CreateProductModelRequest("Robotik Eğitim Kiti", $"KIT-{Guid.NewGuid():N}"), cancellationToken);
        var motor = await PostAsync<ComponentResponse>("/api/components",
            new CreateComponentRequest("DC Motor", $"MTR-{Guid.NewGuid():N}", "adet", 2, "https://example.com/dc-motor.png"), cancellationToken);
        var controller = await PostAsync<ComponentResponse>("/api/components",
            new CreateComponentRequest("Kontrol Kartı", $"CTL-{Guid.NewGuid():N}", "adet", 1), cancellationToken);
        var cable = await PostAsync<ComponentResponse>("/api/components",
            new CreateComponentRequest("Bağlantı Kablosu", $"CBL-{Guid.NewGuid():N}", "adet", 5), cancellationToken);
        var shelfA = await PostAsync<StorageLocationResponse>("/api/storage-locations",
            new CreateStorageLocationRequest($"A-{Guid.NewGuid():N}", "Ana Depo", "A", "01", "01"), cancellationToken);
        var shelfB = await PostAsync<StorageLocationResponse>("/api/storage-locations",
            new CreateStorageLocationRequest($"B-{Guid.NewGuid():N}", "Ana Depo", "B", "02", "03"), cancellationToken);

        await PostAsync<StockMovementResponse>("/api/component-stock/receipts",
            new RecordComponentStockRequest(motor.Id, shelfA.Id, 10, "Motor satın alma girişi"), cancellationToken);
        await PostAsync<StockMovementResponse>("/api/component-stock/receipts",
            new RecordComponentStockRequest(controller.Id, shelfA.Id, 3, "Kontrol kartı satın alma girişi"), cancellationToken);
        var transfer = await PostAsync<StockMovementResponse[]>("/api/component-stock/transfers",
            new TransferComponentStockRequest(motor.Id, shelfA.Id, shelfB.Id, 2, "Atölye rafına transfer"), cancellationToken);

        await PostAsync<BillOfMaterialsResponse>($"/api/product-models/{product.Id}/bom",
            new CreateBillOfMaterialsRequest(1,
            [
                new BillOfMaterialsLineRequest(motor.Id, 2),
                new BillOfMaterialsLineRequest(controller.Id, 1)
            ]), cancellationToken);

        var buildable = await _client.GetFromJsonAsync<BuildableKitResponse>(
            $"/api/manufacturing/buildable-kits/{product.Id}", cancellationToken);
        var motorStocks = await _client.GetFromJsonAsync<ComponentStockResponse[]>(
            $"/api/component-stock?componentId={motor.Id}", cancellationToken);
        var lowStock = await _client.GetFromJsonAsync<ComponentResponse[]>("/api/components/low-stock", cancellationToken);
        var suggestions = await _client.GetFromJsonAsync<ComponentSearchResponse[]>("/api/components/search?query=Motor", cancellationToken);
        var locator = await _client.GetFromJsonAsync<ComponentLocatorResponse>($"/api/components/{motor.Id}/locator", cancellationToken);

        Assert.Equal(2, transfer.Length);
        Assert.NotEqual(Guid.Empty, transfer[0].TransferId);
        Assert.Equal(transfer[0].TransferId, transfer[1].TransferId);
        Assert.Equal(3, buildable!.BuildableQuantity);
        var bottleneck = buildable.Components.Single(item => item.ComponentId == controller.Id);
        Assert.True(bottleneck.IsBottleneck);
        Assert.Equal(1, bottleneck.MissingForNextKit);
        Assert.Equal("adet", bottleneck.UnitOfMeasure);
        Assert.Equal(8, motorStocks!.Single(item => item.StorageLocationId == shelfA.Id).Quantity);
        Assert.Equal(2, motorStocks!.Single(item => item.StorageLocationId == shelfB.Id).Quantity);
        Assert.Contains(lowStock!, item => item.Id == cable.Id && item.TotalStock == 0);
        Assert.Equal("https://example.com/dc-motor.png", suggestions!.Single(item => item.Id == motor.Id).ImageUrl);
        Assert.Equal(10, locator!.TotalStock);
        Assert.Equal(2, locator.Locations.Count);
    }

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken cancellationToken)
    {
        var response = await _client.PostAsJsonAsync(path, body, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(cancellationToken))!;
    }
}
