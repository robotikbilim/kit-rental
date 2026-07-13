using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using KitRental.Core.Application.Inventory;
using KitRental.Core.Application.Rentals;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Orders;
using KitRental.Security;

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
    public async Task FullRentalLifecycle_ReturnsUnitToAvailableOnlyAfterInspection()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var model = await PostAsync<ProductModelResponse>(
            "/api/product-models", new CreateProductModelRequest("Yaşam Döngüsü Seti", $"LC-{Guid.NewGuid():N}"), cancellationToken);
        var unit = await PostAsync<ProductUnitResponse>(
            "/api/product-units", new CreateProductUnitRequest(model.Id, $"SN-{Guid.NewGuid():N}", $"QR-{Guid.NewGuid():N}"), cancellationToken);
        var customer = await PostAsync<CustomerResponse>(
            "/api/customers",
            new CreateCustomerRequest("Yaşam Döngüsü Okulu", $"lc-{Guid.NewGuid():N}@example.com",
                new AddressRequest("Okul", "Teslim Alan", "5550001122", "Bilim Sokak 1", "Nilüfer", "Bursa", "16000")),
            cancellationToken);
        var start = new DateOnly(2026, 9, 1);
        var end = new DateOnly(2026, 9, 15);
        var order = await PostAsync<OrderResponse>(
            "/api/orders",
            new CreateOrderRequest(customer.Id, customer.Addresses.Single().Id, start, end, [new OrderLineRequest(model.Id, 1)]),
            cancellationToken);
        await PostAsync<OrderResponse>($"/api/orders/{order.Id}/transitions", new OrderTransitionRequest(RentalOrderStatus.Approved), cancellationToken);
        await PostAsync<RentalAssignmentResponse>(
            "/api/rental-assignments",
            new CreateRentalAssignmentRequest(order.Lines.Single().Id, customer.Id, unit.Id, start, end),
            cancellationToken);
        await PostAsync<OrderResponse>($"/api/orders/{order.Id}/transitions", new OrderTransitionRequest(RentalOrderStatus.Preparing), cancellationToken);
        await PostAsync<OrderResponse>($"/api/orders/{order.Id}/transitions", new OrderTransitionRequest(RentalOrderStatus.ReadyToShip), cancellationToken);

        var outbound = await PostAsync<ShipmentResponse>(
            "/api/shipments",
            new CreateShipmentRequest(order.Id, null, ShipmentType.Outbound, "Test Kargo", $"OUT-{Guid.NewGuid():N}"),
            cancellationToken);
        await PostAsync<ShipmentResponse>(
            $"/api/shipments/{outbound.Id}/events",
            new ShipmentEventRequest(ShipmentStatus.Delivered, DateTimeOffset.UtcNow, "Bursa", "Müşteriye teslim edildi."),
            cancellationToken);
        await PostAsync<OrderResponse>($"/api/orders/{order.Id}/transitions", new OrderTransitionRequest(RentalOrderStatus.AwaitingReturn), cancellationToken);

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

        var units = await _client.GetFromJsonAsync<ProductUnitResponse[]>("/api/product-units", cancellationToken);
        Assert.Equal(ProductUnitStatus.Available, units!.Single(item => item.Id == unit.Id).Status);

        var audit = await _client.GetFromJsonAsync<AuditResponse[]>("/api/audit", cancellationToken);
        Assert.True(audit!.Length >= 8);
        var report = await _client.GetStringAsync("/api/reports/inventory.csv", cancellationToken);
        Assert.Contains(unit.SerialNumber, report, StringComparison.Ordinal);
    }

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken cancellationToken)
    {
        var response = await _client.PostAsJsonAsync(path, body, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(cancellationToken))!;
    }

    private sealed record CustomerResponse(Guid Id, IReadOnlyCollection<AddressResponse> Addresses);
    private sealed record AddressResponse(Guid Id);
    private sealed record OrderResponse(Guid Id, IReadOnlyCollection<OrderLineResponse> Lines);
    private sealed record OrderLineResponse(Guid Id);
    private sealed record ShipmentResponse(Guid Id);
    private sealed record InspectionResponse(Guid Id);
    private sealed record AuditResponse(Guid Id, string Action);
}
