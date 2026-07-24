using System.Net.Http.Headers;
using System.Net.Http.Json;
using KitRental.Core.Application.CustomerPortal;
using KitRental.Core.Application.Inventory;
using KitRental.Core.Application.Operations;
using KitRental.Core.Application.PhysicalKits;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Returns;
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
        var deliverableUnit = await PostAsync<ProductUnitResponse>(admin, "/api/product-units",
            new CreateProductUnitRequest(model.Id, $"PT-DEL-{Guid.NewGuid():N}", $"PT-DEL-QR-{Guid.NewGuid():N}"), cancellationToken);
        var email = $"tacev-{Guid.NewGuid():N}@example.com";
        var rental = await PostAsync<RentPhysicalKitResponse>(admin, $"/api/physical-kits/{unit.Id}/rent",
            new RentPhysicalKitRequest("TACEV Test Merkezi", email, "02165550000", "Bilim Sokak 1",
                "Kadıköy", "İstanbul", "34000", new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1)), cancellationToken);

        var customer = CreateClient(new TokenUser(Guid.NewGuid(), email, "CustomerAccountManager", rental.CustomerId));
        var overview = await customer.GetFromJsonAsync<CustomerPortalResponse>("/api/customer-portal", cancellationToken);
        Assert.Equal(unit.Id, overview!.Kits.Single().ProductUnitId);
        var forbiddenPurchase = await customer.PostAsJsonAsync("/api/purchase-orders",
            new CreatePurchaseOrderRequest(rental.CustomerId, overview.Addresses.Single().Id,
                [new OrderLineRequest(model.Id, 1)]), cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, forbiddenPurchase.StatusCode);

        var request = await customer.PostAsJsonAsync("/api/customer-portal/rental-requests", new PortalRentalRequest(
            overview.Addresses.Single().Id, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1),
            [new OrderLineRequest(model.Id, 1)]), cancellationToken);
        request.EnsureSuccessStatusCode();
        var deliveryOrder = (await request.Content.ReadFromJsonAsync<CreatedOrderResponse>(cancellationToken))!;
        Assert.Equal(OrderType.Rental, deliveryOrder.Type);

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

        await PostAsync<OrderResponse>(admin, $"/api/orders/{deliveryOrder.Id}/transitions",
            new OrderTransitionRequest(RentalOrderStatus.Approved), cancellationToken);
        await PostAsync<OrderKitPreparationResponse>(admin, $"/api/orders/{deliveryOrder.Id}/kits",
            new { lines = new[] { new { productModelId = model.Id, quantity = 1 } }, useAvailableKits = true }, cancellationToken);
        await PostAsync<OrderResponse>(admin, $"/api/orders/{deliveryOrder.Id}/transitions",
            new OrderTransitionRequest(RentalOrderStatus.Preparing), cancellationToken);
        await PostAsync<OrderResponse>(admin, $"/api/orders/{deliveryOrder.Id}/transitions",
            new OrderTransitionRequest(RentalOrderStatus.OutboundInTransit), cancellationToken);

        var otherCustomer = CreateClient(new TokenUser(Guid.NewGuid(), "other@portal.test", "CustomerUser", Guid.NewGuid()));
        var forbiddenConfirmation = await otherCustomer.PostAsJsonAsync(
            $"/api/customer-portal/orders/{deliveryOrder.Id}/confirm-delivery", new { }, cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, forbiddenConfirmation.StatusCode);

        var confirmation = await customer.PostAsJsonAsync(
            $"/api/customer-portal/orders/{deliveryOrder.Id}/confirm-delivery", new { }, cancellationToken);
        confirmation.EnsureSuccessStatusCode();
        var confirmedOrder = (await confirmation.Content.ReadFromJsonAsync<OrderResponse>(cancellationToken))!;
        Assert.Equal(RentalOrderStatus.Completed, confirmedOrder.Status);

        overview = await customer.GetFromJsonAsync<CustomerPortalResponse>("/api/customer-portal", cancellationToken);
        Assert.Contains(overview!.Orders, item => item.Id == deliveryOrder.Id && item.Status == RentalOrderStatus.Completed);
        Assert.Contains(overview.Kits, item => item.ProductUnitId == deliverableUnit.Id &&
            item.UnitStatus == KitRental.Core.Domain.Inventory.ProductUnitStatus.WithCustomer);

        var adminOrders = await admin.GetFromJsonAsync<PortalOrderResponse[]>("/api/order-summaries", cancellationToken);
        Assert.Contains(adminOrders!, item => item.Id == deliveryOrder.Id && item.Status == RentalOrderStatus.Completed);
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

    [Fact]
    public async Task Customer_CanReturnExpiredSelectedKit_AndAdminReceivesItIntoAvailableStock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var admin = CreateClient(new TokenUser(Guid.NewGuid(), "admin@return.test", "SystemAdmin", null));
        var model = await PostAsync<ProductModelResponse>(admin, "/api/product-models",
            new CreateProductModelRequest("İade Test Kiti", $"RET-{Guid.NewGuid():N}"), cancellationToken);
        var unit = await PostAsync<ProductUnitResponse>(admin, "/api/product-units",
            new CreateProductUnitRequest(model.Id, $"RET-SN-{Guid.NewGuid():N}", $"RET-QR-{Guid.NewGuid():N}"), cancellationToken);
        var expiringUnit = await PostAsync<ProductUnitResponse>(admin, "/api/product-units",
            new CreateProductUnitRequest(model.Id, $"EXP-SN-{Guid.NewGuid():N}", $"EXP-QR-{Guid.NewGuid():N}"), cancellationToken);
        var email = $"return-{Guid.NewGuid():N}@example.com";
        var rental = await PostAsync<RentPhysicalKitResponse>(admin, $"/api/physical-kits/{unit.Id}/rent",
            new RentPhysicalKitRequest("İade Müşterisi", email, "02120000000", "Test Sokak 1",
                "Kadıköy", "İstanbul", "34000", today.AddMonths(-2), today.AddDays(-1)), cancellationToken);
        var customer = CreateClient(new TokenUser(Guid.NewGuid(), email, "CustomerAccountManager", rental.CustomerId));
        await PostAsync<RentPhysicalKitResponse>(admin, $"/api/physical-kits/{expiringUnit.Id}/rent",
            new RentPhysicalKitRequest("Yaklaşan Kiralama", $"expiring-{Guid.NewGuid():N}@example.com", "02120000001",
                "Test Sokak 2", "Kadıköy", "İstanbul", "34000", today.AddDays(-10), today.AddDays(7)), cancellationToken);

        var expiryDashboard = await admin.GetFromJsonAsync<DashboardResponse>("/api/dashboard", cancellationToken);
        Assert.Contains(expiryDashboard!.ExpiredRentalKits, x => x.ProductUnitId == unit.Id && x.DaysRemaining < 0);
        Assert.Contains(expiryDashboard.ExpiringRentalKits, x => x.ProductUnitId == expiringUnit.Id && x.DaysRemaining == 7);

        var created = await PostAsync<ReturnResponse>(customer, "/api/customer-portal/returns",
            new { assignmentIds = new[] { rental.AssignmentId } }, cancellationToken);
        Assert.Equal(KitReturnStatus.Requested, created.Status);
        var shipped = await PostAsync<ReturnResponse>(customer, $"/api/customer-portal/returns/{created.Id}/ship",
            new { carrier = "Test Kargo", trackingNumber = $"TK-{Guid.NewGuid():N}" }, cancellationToken);
        Assert.Equal(KitReturnStatus.InTransit, shipped.Status);

        var dashboard = await admin.GetFromJsonAsync<DashboardResponse>("/api/dashboard", cancellationToken);
        Assert.Contains(dashboard!.ReturnsInProgress, x => x.Id == created.Id && x.KitCount == 1);
        await PostAsync<ReturnResponse>(admin, $"/api/kit-returns/{created.Id}/receive", new { }, cancellationToken);

        var units = await admin.GetFromJsonAsync<ProductUnitResponse[]>("/api/product-units", cancellationToken);
        Assert.Equal(ProductUnitStatus.Available, units!.Single(x => x.Id == unit.Id).Status);
        var overview = await customer.GetFromJsonAsync<CustomerPortalResponse>("/api/customer-portal", cancellationToken);
        Assert.Contains(overview!.Returns, x => x.Id == created.Id && x.Status == KitReturnStatus.Received);
    }

    private sealed record CreatedFaultResponse(Guid Id);
    private sealed record CreatedOrderResponse(Guid Id, OrderType Type);
    private sealed record OrderResponse(Guid Id, RentalOrderStatus Status);
    private sealed record ReturnResponse(Guid Id, KitReturnStatus Status);
}
