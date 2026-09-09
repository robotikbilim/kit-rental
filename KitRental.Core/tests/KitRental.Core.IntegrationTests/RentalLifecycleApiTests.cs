using KitRental.Core.Application.Inventory;
using KitRental.Core.Application.Operations;
using KitRental.Core.Application.Workshop;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Orders;
using KitRental.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace KitRental.Core.IntegrationTests;

public sealed class RentalLifecycleApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public RentalLifecycleApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing")).CreateClient();
        var tokens = new TokenService(new TokenOptions(
            "KitRental.Identity", "KitRental", "development-only-secret-change-before-production-2026", TimeSpan.FromHours(8)));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            tokens.Create(new TokenUser(Guid.NewGuid(), "lifecycle@test.local", "SystemAdmin", null), DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task FullRentalLifecycleReturnsUnitToAvailableOnlyAfterInspection()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var model = await PostAsync<ProductModelResponse>(
            "/api/product-models", new CreateProductModelRequest("Yaşam Döngüsü Seti", $"LC-{Guid.NewGuid():N}"), cancellationToken);
        var unit = await PostAsync<ProductUnitResponse>(
            "/api/product-units", new CreateProductUnitRequest(model.Id, $"SN-{Guid.NewGuid():N}", $"QR-{Guid.NewGuid():N}"), cancellationToken);
        var customer = await PostAsync<CustomerResponse>(
            "/api/customers",
            new CreateCustomerRequest("Yaşam Döngüsü Okulu", $"lc-{Guid.NewGuid():N}@example.com",
                new AddressRequest("Okul", "Teslim Alan", "5550001122", "Bilim Sokak 1", "16000")),
            cancellationToken);
        var start = new DateOnly(2026, 9, 1);
        var end = new DateOnly(2026, 9, 15);
        var order = await PostAsync<OrderResponse>(
            "/api/orders",
            new CreateOrderRequest(customer.Id, model.Id, start, end,
                [new CreateOrderStudentRequest("Teslim Alan", "5550001122")]),
            cancellationToken);
        await PostAsync<OrderResponse>($"/api/orders/{order.Id}/status-transitions", new OrderTransitionRequest(RentalOrderStatus.Approved), cancellationToken);
        await CompleteStudentAddressesAsync(order.Id, "Bilim Sokak 1", cancellationToken);
        await PostAsync<OrderKitPreparationResponse>(
            $"/api/orders/{order.Id}/kits",
            new { lines = new[] { new { productModelId = model.Id, quantity = 1 } }, useAvailableKits = true },
            cancellationToken);
        await PostAsync<OrderResponse>($"/api/orders/{order.Id}/status-transitions", new OrderTransitionRequest(RentalOrderStatus.Preparing), cancellationToken);
        await PostAsync<OrderResponse>($"/api/orders/{order.Id}/status-transitions", new OrderTransitionRequest(RentalOrderStatus.ReadyToShip), cancellationToken);

        var outbound = await PostAsync<ShipmentResponse>(
            "/api/shipments",
            new CreateShipmentRequest(order.Id, null, ShipmentType.Outbound, "Test Kargo", $"OUT-{Guid.NewGuid():N}"),
            cancellationToken);
        await PostAsync<ShipmentResponse>(
            $"/api/shipments/{outbound.Id}/events",
            new ShipmentEventRequest(ShipmentStatus.Delivered, DateTimeOffset.UtcNow, "Bursa", "Müşteriye teslim edildi."),
            cancellationToken);
        await PostAsync<OrderResponse>($"/api/orders/{order.Id}/status-transitions", new OrderTransitionRequest(RentalOrderStatus.AwaitingReturn), cancellationToken);

        var inbound = await PostAsync<ShipmentResponse>(
            "/api/shipments",
            new CreateShipmentRequest(order.Id, null, ShipmentType.Return, "Test Kargo", $"RET-{Guid.NewGuid():N}"),
            cancellationToken);
        await PostAsync<ShipmentResponse>(
            $"/api/shipments/{inbound.Id}/events",
            new ShipmentEventRequest(ShipmentStatus.Delivered, DateTimeOffset.UtcNow, "Depo", "İade depoya teslim edildi."),
            cancellationToken);
        await PostAsync<InspectionResponse>(
            "/api/return-inspections",
            new CompleteInspectionRequest(order.Id, unit.Id, [new InspectionItemRequest("Ana set", true, false, "Eksiksiz")], 0, ProductUnitStatus.Available),
            cancellationToken);

        var units = (await _client.GetFromJsonAsync<PagedResponse<ProductUnitResponse>>("/api/product-units?pageSize=5000", cancellationToken))!.Items;
        Assert.Equal(ProductUnitStatus.Available, units!.Single(item => item.Id == unit.Id).Status);

        var audit = await _client.GetFromJsonAsync<AuditResponse[]>("/api/audit", cancellationToken);
        Assert.True(audit!.Length >= 8);
        var report = await _client.GetStringAsync("/api/reports/inventory.csv", cancellationToken);
        Assert.Contains(unit.SerialNumber, report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OrderScreenFlowAdvancesOrderAndAssignedUnitThroughDelivery()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var model = await PostAsync<ProductModelResponse>(
            "/api/product-models", new CreateProductModelRequest("Sipariş Akış Seti", $"OF-{Guid.NewGuid():N}"), cancellationToken);
        var location = await PostAsync<StorageLocationResponse>("/api/storage-locations",
            new CreateStorageLocationRequest($"ORDER-{Guid.NewGuid():N}", "Sipariş Deposu", "A", "01", "01"), cancellationToken);
        var component = await PostAsync<ComponentResponse>("/api/components",
            new CreateComponentRequest("Sipariş Kit Komponenti", $"ORD-CMP-{Guid.NewGuid():N}", "adet", 0, null, location.Id),
            cancellationToken);
        await PostAsync<StockMovementResponse>("/api/component-stock/receipts",
            new RecordComponentStockRequest(component.Id, location.Id, 3, "Sipariş üretim stoğu"), cancellationToken);
        await PostAsync<BillOfMaterialsResponse>($"/api/product-models/{model.Id}/bom",
            new CreateBillOfMaterialsRequest(1, [new BillOfMaterialsLineRequest(component.Id, 1)]), cancellationToken);
        var readyUnit = await PostAsync<ProductUnitResponse>("/api/product-units",
            new CreateProductUnitRequest(model.Id, $"READY-{Guid.NewGuid():N}", $"READY-QR-{Guid.NewGuid():N}"),
            cancellationToken);
        var customer = await PostAsync<CustomerResponse>(
            "/api/customers",
            new CreateCustomerRequest("Akış Test Okulu", $"flow-{Guid.NewGuid():N}@example.com",
                new AddressRequest("Okul", "Teslim Alan", "5550007788", "Test Sokak 1", "06000")),
            cancellationToken);
        var start = new DateOnly(2026, 10, 1);
        var end = new DateOnly(2026, 10, 15);
        var order = await PostAsync<OrderResponse>("/api/orders",
            new CreateOrderRequest(customer.Id, model.Id, start, end,
                [
                    new CreateOrderStudentRequest("Birinci Öğrenci", "5550007788"),
                    new CreateOrderStudentRequest("İkinci Öğrenci", "5550007799")
                ]), cancellationToken);
        await CompleteStudentAddressesAsync(order.Id, "Test Sokak 1", cancellationToken);

        order = await PostAsync<OrderResponse>($"/api/orders/{order.Id}/status-transitions",
            new OrderTransitionRequest(RentalOrderStatus.Approved), cancellationToken);
        var prepared = await PostAsync<OrderKitPreparationResponse>($"/api/orders/{order.Id}/kits",
            new { lines = new[] { new { productModelId = model.Id, quantity = 2 } }, useAvailableKits = true },
            cancellationToken);
        Assert.Equal(1, prepared.CreatedCount);
        Assert.Equal(1, prepared.ReusedCount);
        Assert.Contains(prepared.Kits, kit => kit.ProductUnitId == readyUnit.Id);
        Assert.All(prepared.Kits, kit => Assert.Equal(ProductUnitStatus.Reserved, kit.Status));
        var componentStocks = (await _client.GetFromJsonAsync<PagedResponse<ComponentStockResponse>>(
            $"/api/component-stock?componentId={component.Id}&pageSize=5000", cancellationToken))!.Items;
        var componentMovements = (await _client.GetFromJsonAsync<PagedResponse<StockMovementResponse>>(
            $"/api/component-stock/movements?componentId={component.Id}&pageSize=5000", cancellationToken))!.Items;
        Assert.Equal(1, componentStocks!.Single().Quantity);
        Assert.Equal(2, componentMovements!.Where(item =>
            item.Type == KitRental.Core.Domain.Warehouse.StockMovementType.Consumption).Sum(item => item.Quantity));
        order = await PostAsync<OrderResponse>($"/api/orders/{order.Id}/status-transitions",
            new OrderTransitionRequest(RentalOrderStatus.Completed), cancellationToken);
        Assert.Equal(RentalOrderStatus.Completed, order.Status);

        var units = (await _client.GetFromJsonAsync<PagedResponse<ProductUnitResponse>>("/api/product-units?pageSize=5000", cancellationToken))!.Items;
        Assert.All(units!.Where(item => prepared.Kits.Any(kit => kit.ProductUnitId == item.Id)),
            item => Assert.Equal(ProductUnitStatus.WithCustomer, item.Status));
    }

    [Fact]
    public async Task PurchaseOrderUsesAvailableUnitsProducesMissingUnitsAndMarksThemSold()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var model = await PostAsync<ProductModelResponse>("/api/product-models",
            new CreateProductModelRequest("Satış Seti", $"SALE-{Guid.NewGuid():N}"), cancellationToken);
        var location = await PostAsync<StorageLocationResponse>("/api/storage-locations",
            new CreateStorageLocationRequest($"SALE-{Guid.NewGuid():N}", "Satış Deposu", "S", "01", "01"),
            cancellationToken);
        var component = await PostAsync<ComponentResponse>("/api/components",
            new CreateComponentRequest("Satış Komponenti", $"SALE-CMP-{Guid.NewGuid():N}", "adet", 0, null,
                location.Id), cancellationToken);
        await PostAsync<StockMovementResponse>("/api/component-stock/receipts",
            new RecordComponentStockRequest(component.Id, location.Id, 2, "Satış üretim stoğu"),
            cancellationToken);
        await PostAsync<BillOfMaterialsResponse>($"/api/product-models/{model.Id}/bom",
            new CreateBillOfMaterialsRequest(1, [new BillOfMaterialsLineRequest(component.Id, 1)]),
            cancellationToken);
        var readyUnit = await PostAsync<ProductUnitResponse>("/api/product-units",
            new CreateProductUnitRequest(model.Id, $"SALE-READY-{Guid.NewGuid():N}",
                $"SALE-READY-QR-{Guid.NewGuid():N}"), cancellationToken);
        var customer = await PostAsync<CustomerResponse>("/api/customers",
            new CreateCustomerRequest("Satış Müşterisi", $"sale-{Guid.NewGuid():N}@example.com",
                new AddressRequest("Merkez", "Teslim Alan", "5550007788", "Satış Sokak 1", "06000")),
            cancellationToken);

        var order = await PostAsync<OrderResponse>("/api/purchase-orders",
            new CreatePurchaseOrderRequest(customer.Id, customer.Addresses.Single().Id,
                [new OrderLineRequest(model.Id, 2)]), cancellationToken);
        Assert.Equal(OrderType.Purchase, order.Type);
        Assert.Equal(RentalOrderStatus.Approved, order.Status);
        Assert.Null(order.Period);
        Assert.StartsWith("SO-", order.OrderNumber);

        var prepared = await PostAsync<OrderKitPreparationResponse>($"/api/orders/{order.Id}/kits",
            new { lines = new[] { new { productModelId = model.Id, quantity = 2 } } }, cancellationToken);
        Assert.Equal(1, prepared.ReusedCount);
        Assert.Equal(1, prepared.CreatedCount);
        Assert.Contains(prepared.Kits, item => item.ProductUnitId == readyUnit.Id);

        order = await PostAsync<OrderResponse>($"/api/orders/{order.Id}/status-transitions",
            new OrderTransitionRequest(RentalOrderStatus.Completed), cancellationToken);
        Assert.Equal(RentalOrderStatus.Completed, order.Status);

        var units = (await _client.GetFromJsonAsync<PagedResponse<ProductUnitResponse>>("/api/product-units?pageSize=5000", cancellationToken))!.Items;
        Assert.All(units!.Where(item => prepared.Kits.Any(kit => kit.ProductUnitId == item.Id)),
            item => Assert.Equal(ProductUnitStatus.Sold, item.Status));

        var detail = await _client.GetFromJsonAsync<OrderDetailResponse>($"/api/orders/{order.Id}",
            cancellationToken);
        Assert.Equal(OrderType.Purchase, detail!.Type);
        Assert.Equal(2, detail.Kits.Count);
    }

    private async Task CompleteStudentAddressesAsync(Guid orderId, string addressLine, CancellationToken cancellationToken)
    {
        var detail = await _client.GetFromJsonAsync<OrderDetailResponse>($"/api/orders/{orderId}",
            cancellationToken);
        foreach (var student in detail!.Students)
        {
            var response = await _client.PostAsJsonAsync(
                $"/api/public/student-addresses/{student.PublicAddressToken}",
                new PublicStudentAddressRequest(addressLine), cancellationToken);
            response.EnsureSuccessStatusCode();
        }
    }

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken cancellationToken)
    {
        var response = await _client.PostAsJsonAsync(path, body, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(cancellationToken))!;
    }

    private sealed record CustomerResponse(Guid Id, IReadOnlyCollection<AddressResponse> Addresses);
    private sealed record AddressResponse(Guid Id);
    private sealed record OrderResponse(Guid Id, string OrderNumber, OrderType Type, RentalOrderStatus Status,
        object? Period, IReadOnlyCollection<OrderLineResponse> Lines);
    private sealed record OrderLineResponse(Guid Id);
    private sealed record ShipmentResponse(Guid Id);
    private sealed record InspectionResponse(Guid Id);
    private sealed record AuditResponse(Guid Id, string Action);
}
