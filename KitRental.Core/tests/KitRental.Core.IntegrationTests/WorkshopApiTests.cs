using System.Net.Http.Headers;
using System.Net.Http.Json;
using KitRental.Core.Application.CustomerPortal;
using KitRental.Core.Application.Inventory;
using KitRental.Core.Application.PhysicalKits;
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
    public async Task CreateKit_WithoutRecipe_AllowsRecipeToBeAddedAndUpdatedLater()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var component = await PostAsync<ComponentResponse>("/api/components",
            new CreateComponentRequest("Sonradan Reçete Komponenti", $"CMP-{Guid.NewGuid():N}", "adet", 1), cancellationToken);
        var kit = await PostAsync<KitCatalogResponse>("/api/kits",
            new CreateKitRequest("Reçetesiz Eğitim Kiti", $"KIT-{Guid.NewGuid():N}", null, null, 1, []), cancellationToken);

        Assert.Null(kit.BomVersion);
        Assert.Empty(kit.Lines);
        var missingBom = await _client.GetAsync($"/api/product-models/{kit.Id}/bom", cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, missingBom.StatusCode);

        await PostAsync<BillOfMaterialsResponse>($"/api/product-models/{kit.Id}/bom",
            new CreateBillOfMaterialsRequest(1, [new BillOfMaterialsLineRequest(component.Id, 2)]), cancellationToken);
        await PostAsync<BillOfMaterialsResponse>($"/api/product-models/{kit.Id}/bom",
            new CreateBillOfMaterialsRequest(2, [new BillOfMaterialsLineRequest(component.Id, 4)]), cancellationToken);

        var activeBom = await _client.GetFromJsonAsync<BillOfMaterialsResponse>(
            $"/api/product-models/{kit.Id}/bom", cancellationToken);
        Assert.Equal(2, activeBom!.Version);
        Assert.Equal(4, activeBom.Lines.Single().Quantity);
    }

    [Fact]
    public async Task GetRecipe_DistinguishesARecipeLessKitFromAnUnknownKit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var kit = await PostAsync<KitCatalogResponse>("/api/kits",
            new CreateKitRequest("Reçetesiz Detay Kiti", $"KIT-{Guid.NewGuid():N}", null, null, 1, []),
            cancellationToken);

        var recipeLessKit = await _client.GetAsync($"/api/product-models/{kit.Id}/bom", cancellationToken);
        var unknownKit = await _client.GetAsync($"/api/product-models/{Guid.NewGuid()}/bom", cancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.NoContent, recipeLessKit.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, unknownKit.StatusCode);
    }

    [Fact]
    public async Task CreatePhysicalKit_WithoutIdentifiers_GeneratesUniqueSerialAndQrCode()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var model = await PostAsync<ProductModelResponse>("/api/product-models",
            new CreateProductModelRequest("Otomatik Seri Test Kiti", $"KIT-{Guid.NewGuid():N}"), cancellationToken);

        var first = await PostAsync<ProductUnitResponse>("/api/product-units",
            new { ProductModelId = model.Id }, cancellationToken);
        var second = await PostAsync<ProductUnitResponse>("/api/product-units",
            new { ProductModelId = model.Id }, cancellationToken);

        Assert.StartsWith("KR-", first.SerialNumber, StringComparison.Ordinal);
        Assert.StartsWith("KR-", second.SerialNumber, StringComparison.Ordinal);
        Assert.NotEqual(first.SerialNumber, second.SerialNumber);
        Assert.Equal($"KITRENTAL:{first.SerialNumber}", first.QrCode);
        Assert.Equal($"KITRENTAL:{second.SerialNumber}", second.QrCode);
    }

    [Fact]
    public async Task PhysicalKitLookup_FindsCompleteKitBySerialNumberOrQrCode()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var model = await PostAsync<ProductModelResponse>("/api/product-models",
            new CreateProductModelRequest("Geçmiş Sorgu Kiti", $"KIT-{Guid.NewGuid():N}"), cancellationToken);
        var unit = await PostAsync<ProductUnitResponse>("/api/product-units",
            new { ProductModelId = model.Id }, cancellationToken);

        var bySerial = await _client.GetFromJsonAsync<KitRental.Core.Application.PhysicalKits.PhysicalKitDetailResponse>(
            $"/api/physical-kits/lookup?identifier={Uri.EscapeDataString(unit.SerialNumber)}", cancellationToken);
        var byQr = await _client.GetFromJsonAsync<KitRental.Core.Application.PhysicalKits.PhysicalKitDetailResponse>(
            $"/api/physical-kits/lookup?identifier={Uri.EscapeDataString(unit.QrCode)}", cancellationToken);

        Assert.Equal(unit.Id, bySerial!.Kit.Id);
        Assert.Equal(unit.Id, byQr!.Kit.Id);
        Assert.NotEmpty(bySerial.StatusHistory);
    }

    [Fact]
    public async Task CreatePhysicalKitsBulk_GeneratesDistinctIdentifiersForEveryUnit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var model = await PostAsync<ProductModelResponse>("/api/product-models",
            new CreateProductModelRequest("Toplu Üretim Test Kiti", $"KIT-{Guid.NewGuid():N}"), cancellationToken);

        var units = await PostAsync<ProductUnitResponse[]>("/api/product-units/bulk",
            new { ProductModelId = model.Id, Quantity = 23 }, cancellationToken);

        Assert.Equal(23, units.Length);
        Assert.Equal(23, units.Select(item => item.SerialNumber).Distinct().Count());
        Assert.Equal(23, units.Select(item => item.QrCode).Distinct().Count());
        Assert.All(units, item => Assert.Equal($"KITRENTAL:{item.SerialNumber}", item.QrCode));

        var summaries = await _client.GetFromJsonAsync<KitRental.Core.Application.PhysicalKits.PhysicalKitModelSummaryResponse[]>(
            "/api/physical-kits/models", cancellationToken);
        var page = await _client.GetFromJsonAsync<KitRental.Core.Application.PhysicalKits.PhysicalKitUnitPageResponse>(
            $"/api/physical-kits/models/{model.Id}/units?filter=available&page=2&pageSize=10", cancellationToken);
        var labels = await _client.GetFromJsonAsync<KitRental.Core.Application.PhysicalKits.PhysicalKitListItemResponse[]>(
            $"/api/physical-kits/models/{model.Id}/labels?filter=all", cancellationToken);

        var summary = summaries!.Single(item => item.ProductModelId == model.Id);
        Assert.Equal(23, summary.Total);
        Assert.Equal(23, summary.Available);
        Assert.Equal(0, summary.Faulty);
        Assert.Equal(2, page!.Page);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(10, page.Items.Count);
        Assert.Equal(23, labels!.Length);

        var invalid = await _client.PostAsJsonAsync("/api/product-units/bulk",
            new { ProductModelId = model.Id, Quantity = 201 }, cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task CreatePhysicalKits_ConsumesRecipeQuantitiesAndRejectsInsufficientStockAtomically()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var shelfA = await PostAsync<StorageLocationResponse>("/api/storage-locations",
            new CreateStorageLocationRequest($"URETIM-A-{Guid.NewGuid():N}", "Üretim Deposu", "A", "01", "01"),
            cancellationToken);
        var shelfB = await PostAsync<StorageLocationResponse>("/api/storage-locations",
            new CreateStorageLocationRequest($"URETIM-B-{Guid.NewGuid():N}", "Üretim Deposu", "B", "01", "01"),
            cancellationToken);
        var component = await PostAsync<ComponentResponse>("/api/components",
            new CreateComponentRequest("Üretim Test Komponenti", $"PRD-{Guid.NewGuid():N}", "adet", 0, null, shelfA.Id),
            cancellationToken);
        await PostAsync<StockMovementResponse>("/api/component-stock/receipts",
            new RecordComponentStockRequest(component.Id, shelfA.Id, 4, "Üretim stoğu A"), cancellationToken);
        await PostAsync<StockMovementResponse>("/api/component-stock/receipts",
            new RecordComponentStockRequest(component.Id, shelfB.Id, 6, "Üretim stoğu B"), cancellationToken);
        var kit = await PostAsync<KitCatalogResponse>("/api/kits",
            new CreateKitRequest("Stok Tüketen Kit", $"KIT-{Guid.NewGuid():N}", null, null, 1,
                [new BillOfMaterialsLineRequest(component.Id, 3)]), cancellationToken);

        var singleUnit = await PostAsync<ProductUnitResponse>("/api/product-units",
            new { ProductModelId = kit.Id }, cancellationToken);
        await PostAsync<ProductUnitResponse[]>("/api/product-units/bulk",
            new { ProductModelId = kit.Id, Quantity = 2 }, cancellationToken);

        var stocks = await _client.GetFromJsonAsync<ComponentStockResponse[]>(
            $"/api/component-stock?componentId={component.Id}", cancellationToken);
        var movements = await _client.GetFromJsonAsync<StockMovementResponse[]>(
            $"/api/component-stock/movements?componentId={component.Id}", cancellationToken);
        Assert.Equal(1, stocks!.Sum(item => item.Quantity));
        Assert.Equal(9, movements!.Where(item => item.Type == KitRental.Core.Domain.Warehouse.StockMovementType.Consumption)
            .Sum(item => item.Quantity));

        var failed = await _client.PostAsJsonAsync("/api/product-units",
            new { ProductModelId = kit.Id }, cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, failed.StatusCode);

        var units = await _client.GetFromJsonAsync<ProductUnitResponse[]>("/api/product-units", cancellationToken);
        var stocksAfterFailure = await _client.GetFromJsonAsync<ComponentStockResponse[]>(
            $"/api/component-stock?componentId={component.Id}", cancellationToken);
        Assert.Equal(3, units!.Count(item => item.ProductModelId == kit.Id));
        Assert.Equal(1, stocksAfterFailure!.Sum(item => item.Quantity));

        var deleted = await _client.DeleteAsync($"/api/product-units/{singleUnit.Id}", cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, deleted.StatusCode);
        var stocksAfterDelete = await _client.GetFromJsonAsync<ComponentStockResponse[]>(
            $"/api/component-stock?componentId={component.Id}", cancellationToken);
        var movementsAfterDelete = await _client.GetFromJsonAsync<StockMovementResponse[]>(
            $"/api/component-stock/movements?componentId={component.Id}", cancellationToken);
        var unitsAfterDelete = await _client.GetFromJsonAsync<ProductUnitResponse[]>("/api/product-units", cancellationToken);
        Assert.Equal(3, stocksAfterDelete!.Single(item => item.StorageLocationId == shelfA.Id).Quantity);
        Assert.Equal(1, stocksAfterDelete!.Single(item => item.StorageLocationId == shelfB.Id).Quantity);
        Assert.Equal(3, movementsAfterDelete!.Where(item =>
            item.Type == KitRental.Core.Domain.Warehouse.StockMovementType.AdjustmentIncrease).Sum(item => item.Quantity));
        Assert.Equal(2, unitsAfterDelete!.Count(item => item.ProductModelId == kit.Id));
    }

    [Fact]
    public async Task BulkRentPhysicalKits_CreatesOneOrderAndActivatesEverySelectedKit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var model = await PostAsync<ProductModelResponse>("/api/product-models",
            new CreateProductModelRequest("Toplu Kiralama Test Kiti", $"KIT-{Guid.NewGuid():N}"), cancellationToken);
        var units = await PostAsync<ProductUnitResponse[]>("/api/product-units/bulk",
            new { ProductModelId = model.Id, Quantity = 3 }, cancellationToken);

        var rental = await PostAsync<BulkRentPhysicalKitsResponse>("/api/physical-kits/bulk-rent",
            new BulkRentPhysicalKitsRequest(units.Select(item => item.Id).ToArray(), "TACEV Toplu Test",
                $"bulk-{Guid.NewGuid():N}@example.com", "02165550000", "Bilim Sokak 1", "Kadıköy",
                "İstanbul", "34000", new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1)), cancellationToken);

        Assert.Equal(3, rental.KitCount);
        Assert.Equal(3, rental.Kits.Count);
        Assert.Equal(3, rental.Kits.Select(item => item.AssignmentId).Distinct().Count());
        Assert.All(rental.Kits, item => Assert.Equal(KitRental.Core.Domain.Inventory.ProductUnitStatus.WithCustomer, item.Status));

        var orders = await _client.GetFromJsonAsync<PortalOrderResponse[]>("/api/order-summaries", cancellationToken);
        var order = orders!.Single(item => item.Id == rental.OrderId);
        Assert.Single(order.Lines);
        Assert.Equal(3, order.Lines.Single().Quantity);

        foreach (var unit in units)
        {
            var detail = await _client.GetFromJsonAsync<PhysicalKitDetailResponse>(
                $"/api/physical-kits/{unit.Id}", cancellationToken);
            Assert.Equal(rental.OrderNumber, detail!.RentalHistory.Single().OrderNumber);
        }

        var available = await _client.GetFromJsonAsync<PhysicalKitUnitPageResponse>(
            $"/api/physical-kits/models/{model.Id}/units?filter=available&page=1&pageSize=20", cancellationToken);
        Assert.Equal(0, available!.TotalCount);
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

    [Fact]
    public async Task ComponentCreationAcceptsInitialStockAndQuickAdjustmentsChangeTotal()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var location = await PostAsync<StorageLocationResponse>("/api/storage-locations",
            new CreateStorageLocationRequest($"QUICK-{Guid.NewGuid():N}", "Hızlı Stok", "A", "01", "01"),
            cancellationToken);
        var component = await PostAsync<ComponentResponse>("/api/components",
            new CreateComponentRequest("Hızlı Stok Komponenti", $"QCK-{Guid.NewGuid():N}", "adet", 0,
                null, location.Id, 5), cancellationToken);

        Assert.Equal(5, component.TotalStock);
        var decreased = await PostAsync<ComponentLocatorResponse>(
            $"/api/components/{component.Id}/stock-adjustments", new AdjustComponentStockRequest(-1), cancellationToken);
        var increased = await PostAsync<ComponentLocatorResponse>(
            $"/api/components/{component.Id}/stock-adjustments", new AdjustComponentStockRequest(1), cancellationToken);

        Assert.Equal(4, decreased.TotalStock);
        Assert.Equal(5, increased.TotalStock);
        Assert.Equal(5, increased.Locations.Single().Quantity);
    }

    [Fact]
    public async Task StorageLocations_CanBeManagedAndUsedAsNewComponentDefault()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var location = await PostAsync<StorageLocationResponse>("/api/storage-locations",
            new CreateStorageLocationRequest($"RAF-{Guid.NewGuid():N}", "Ana Depo", "C", "04", "02", true),
            cancellationToken);
        var component = await PostAsync<ComponentResponse>("/api/components",
            new CreateComponentRequest("Raflı Komponent", $"CMP-{Guid.NewGuid():N}", "adet", 3),
            cancellationToken);

        Assert.True(location.IsDefaultForNewComponents);
        Assert.Equal(location.Id, component.DefaultStorageLocationId);
        var locator = await _client.GetFromJsonAsync<ComponentLocatorResponse>(
            $"/api/components/{component.Id}/locator", cancellationToken);
        Assert.Equal(location.Id, locator!.Locations.Single().StorageLocationId);
        Assert.Equal(0, locator.Locations.Single().Quantity);

        var updatedResponse = await _client.PutAsJsonAsync($"/api/storage-locations/{location.Id}",
            new CreateStorageLocationRequest(location.Code, "Atölye Deposu", "D", "05", "03", true), cancellationToken);
        updatedResponse.EnsureSuccessStatusCode();
        var updated = (await updatedResponse.Content.ReadFromJsonAsync<StorageLocationResponse>(cancellationToken))!;
        Assert.Equal("Atölye Deposu", updated.Warehouse);
        Assert.True(updated.IsDefaultForNewComponents);

        var secondLocation = await PostAsync<StorageLocationResponse>("/api/storage-locations",
            new CreateStorageLocationRequest($"RAF-{Guid.NewGuid():N}", "İkinci Depo", "E", "01", "01", true),
            cancellationToken);
        var locations = await _client.GetFromJsonAsync<StorageLocationResponse[]>(
            "/api/storage-locations", cancellationToken);
        Assert.False(locations!.Single(item => item.Id == location.Id).IsDefaultForNewComponents);
        Assert.True(locations!.Single(item => item.Id == secondLocation.Id).IsDefaultForNewComponents);
        var secondComponent = await PostAsync<ComponentResponse>("/api/components",
            new CreateComponentRequest("İkinci Varsayılan Raf Komponenti", $"CMP-{Guid.NewGuid():N}", "adet", 0),
            cancellationToken);
        Assert.Equal(secondLocation.Id, secondComponent.DefaultStorageLocationId);

        var deleteResponse = await _client.DeleteAsync($"/api/storage-locations/{location.Id}", cancellationToken);
        deleteResponse.EnsureSuccessStatusCode();
        var deleteSecondResponse = await _client.DeleteAsync(
            $"/api/storage-locations/{secondLocation.Id}", cancellationToken);
        deleteSecondResponse.EnsureSuccessStatusCode();
        var components = await _client.GetFromJsonAsync<ComponentResponse[]>("/api/components", cancellationToken);
        Assert.Null(components!.Single(item => item.Id == component.Id).DefaultStorageLocationId);
        Assert.Null(components!.Single(item => item.Id == secondComponent.Id).DefaultStorageLocationId);

        var unknownShelfComponent = await PostAsync<ComponentResponse>("/api/components",
            new CreateComponentRequest("Rafsız Komponent", $"CMP-{Guid.NewGuid():N}", "adet", 0), cancellationToken);
        Assert.Null(unknownShelfComponent.DefaultStorageLocationId);
    }

    [Fact]
    public async Task DeleteStorageLocation_WithStockHistory_IsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var location = await PostAsync<StorageLocationResponse>("/api/storage-locations",
            new CreateStorageLocationRequest($"DOLU-{Guid.NewGuid():N}", "Ana Depo", "E", "01", "01"),
            cancellationToken);
        var component = await PostAsync<ComponentResponse>("/api/components",
            new CreateComponentRequest("Stoklu Komponent", $"CMP-{Guid.NewGuid():N}", "adet", 0), cancellationToken);
        await PostAsync<StockMovementResponse>("/api/component-stock/receipts",
            new RecordComponentStockRequest(component.Id, location.Id, 1, "Silme koruması testi"), cancellationToken);

        var response = await _client.DeleteAsync($"/api/storage-locations/{location.Id}", cancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
    }

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken cancellationToken)
    {
        var response = await _client.PostAsJsonAsync(path, body, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(cancellationToken))!;
    }
}
