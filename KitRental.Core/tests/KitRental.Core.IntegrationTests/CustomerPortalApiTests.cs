using System.Net.Http.Headers;
using System.Net.Http.Json;
using KitRental.Core.Application.CustomerPortal;
using KitRental.Core.Application.Inventory;
using KitRental.Core.Application.Operations;
using KitRental.Core.Application.PhysicalKits;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Support;
using KitRental.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KitRental.Core.IntegrationTests;

public sealed class CustomerPortalApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly TokenService _tokens = new(new TokenOptions(
        "KitRental.Identity", "KitRental", "development-only-secret-change-before-production-2026", TimeSpan.FromHours(8)));

    public CustomerPortalApiTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));

    [Fact]
    public async Task CustomerPortal_ListsOwnKit_CreatesRequestAndFault()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var admin = CreateClient(new TokenUser(Guid.NewGuid(), "admin@portal.test", "SystemAdmin", null));
        var model = await PostAsync<ProductModelResponse>(admin, "/api/product-models",
            new CreateProductModelRequest("Portal Test Kiti", $"PT-{Guid.NewGuid():N}"), cancellationToken);
        var unit = await PostAsync<ProductUnitResponse>(admin, "/api/product-units",
            new CreateProductUnitRequest(model.Id, $"PT-SN-{Guid.NewGuid():N}", $"PT-QR-{Guid.NewGuid():N}"), cancellationToken);
        var email = $"tacev-{Guid.NewGuid():N}@example.com";
        var rental = await PostAsync<RentPhysicalKitResponse>(admin, $"/api/physical-kits/{unit.Id}/rent",
            new RentPhysicalKitRequest("TACEV Test Merkezi", email, "02165550000", "Bilim Sokak 1",
                "Kadıköy", "İstanbul", "34000", new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1)), cancellationToken);

        var customer = CreateClient(new TokenUser(Guid.NewGuid(), email, "CustomerAccountManager", rental.CustomerId));
        var overview = await customer.GetFromJsonAsync<CustomerPortalResponse>("/api/customer-portal", cancellationToken);
        Assert.Equal(unit.Id, overview!.Kits.Single().ProductUnitId);

        var request = await customer.PostAsJsonAsync("/api/customer-portal/rental-requests", new PortalRentalRequest(
            overview.Addresses.Single().Id, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1),
            [new OrderLineRequest(model.Id, 2)]), cancellationToken);
        request.EnsureSuccessStatusCode();

        var fault = await customer.PostAsJsonAsync("/api/customer-portal/faults", new PortalFaultRequest(
            rental.AssignmentId, "Motor", FaultSeverity.High, "Sol motor yük altında dönmüyor."), cancellationToken);
        fault.EnsureSuccessStatusCode();
        var createdFault = (await fault.Content.ReadFromJsonAsync<CreatedFaultResponse>(cancellationToken))!;

        var faultPage = await admin.GetFromJsonAsync<FaultPageResponse>(
            "/api/faults/search?page=1&pageSize=10&status=1&query=02165550000", cancellationToken);
        var listedFault = Assert.Single(faultPage!.Items, item => item.Id == createdFault.Id);
        Assert.Equal("TACEV Test Merkezi", listedFault.ReporterName);
        Assert.Equal("02165550000", listedFault.ReporterPhone);

        overview = await customer.GetFromJsonAsync<CustomerPortalResponse>("/api/customer-portal", cancellationToken);
        Assert.Contains(overview!.Orders, item => item.Status == RentalOrderStatus.PendingApproval);
        Assert.Contains(overview.Faults, item => item.ProductUnitId == unit.Id && item.Status == FaultStatus.Open);

        var adminOrders = await admin.GetFromJsonAsync<PortalOrderResponse[]>("/api/order-summaries", cancellationToken);
        Assert.Contains(adminOrders!, item => item.CustomerId == rental.CustomerId && item.Status == RentalOrderStatus.PendingApproval);
    }

    private HttpClient CreateClient(TokenUser user)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _tokens.Create(user, DateTimeOffset.UtcNow));
        return client;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string path, object body, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(path, body, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(cancellationToken))!;
    }

    private sealed record CreatedFaultResponse(Guid Id);
}
