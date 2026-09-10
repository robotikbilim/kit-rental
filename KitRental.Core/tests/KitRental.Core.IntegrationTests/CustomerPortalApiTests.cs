using KitRental.Core.Application.CustomerPortal;
using KitRental.Core.Application.Inventory;
using KitRental.Core.Application.Operations;
using KitRental.Core.Application.PhysicalKits;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Returns;
using KitRental.Core.Domain.Support;
using KitRental.Security;
using KitRental.SharedKernel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace KitRental.Core.IntegrationTests;

public sealed class CustomerPortalApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly TokenService _tokens = new(new TokenOptions(
        "KitRental.Identity", "KitRental", "development-only-secret-change-before-production-2026", TimeSpan.FromHours(8)));

    public CustomerPortalApiTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));

    [Fact]
    public async Task CustomerPortalListsOwnKitBlocksRentalRequestAndCreatesFault()
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
        var rental = await PostAsync<RentPhysicalKitResponse>(admin, $"/api/physical-kits/{unit.Id}/rentals",
            new RentPhysicalKitRequest("TACEV Test Merkezi", email, "02165550000", "Bilim Sokak 1",
                "34000", new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1)), cancellationToken);

        var customer = CreateClient(new TokenUser(Guid.NewGuid(), email, "CustomerAccountManager", rental.CustomerId));
        var overview = await customer.GetFromJsonAsync<CustomerPortalResponse>("/api/customer-portal", cancellationToken);
        Assert.Equal(unit.Id, overview!.Kits.Single().ProductUnitId);
        var forbiddenPurchase = await customer.PostAsJsonAsync("/api/purchase-orders",
            new CreatePurchaseOrderRequest(rental.CustomerId, overview.Addresses.Single().Id,
                [new OrderLineRequest(model.Id, 1)]), cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, forbiddenPurchase.StatusCode);

        var blockedRequest = await customer.PostAsJsonAsync("/api/customer-portal/rental-requests", new
        {
            addressId = overview.Addresses.Single().Id,
            startDate = new DateOnly(2026, 11, 1),
            endDate = new DateOnly(2026, 12, 1),
            lines = new[] { new OrderLineRequest(model.Id, 1) }
        }, cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, blockedRequest.StatusCode);

        var deliveryOrder = await PostAsync<CreatedOrderResponse>(admin, "/api/orders", new CreateOrderRequest(
            rental.CustomerId, model.Id, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1),
            [new CreateOrderStudentRequest("Teslim Öğrenci", "05320000000")]), cancellationToken);
        Assert.Equal(OrderType.Rental, deliveryOrder.Type);

        var fault = await customer.PostAsJsonAsync("/api/customer-portal/faults", new PortalFaultRequest(
            rental.AssignmentId, "TACEV Test Merkezi", "02165550000", "Test Sokak 1",
            "Sol motor yük altında dönmüyor."), cancellationToken);
        fault.EnsureSuccessStatusCode();
        var createdFault = (await fault.Content.ReadFromJsonAsync<CreatedFaultResponse>(cancellationToken))!;

        var faultPage = await admin.GetFromJsonAsync<FaultPageResponse>(
            "/api/faults?page=1&pageSize=10&status=1&query=TACEV%20Test%20Merkezi", cancellationToken);
        var listedFault = Assert.Single(faultPage!.Items, item => item.Id == createdFault.Id);
        Assert.Equal("TACEV Test Merkezi", listedFault.ReporterName);
        Assert.Equal("(0216) 555 00 00", listedFault.ReporterPhone);

        overview = await customer.GetFromJsonAsync<CustomerPortalResponse>("/api/customer-portal", cancellationToken);
        Assert.Contains(overview!.Orders, item => item.Status == RentalOrderStatus.PendingApproval);
        Assert.Contains(overview.Faults, item => item.ProductUnitId == unit.Id && item.Status == FaultStatus.Open);

        await PostAsync<OrderResponse>(admin, $"/api/orders/{deliveryOrder.Id}/status-transitions",
            new OrderTransitionRequest(RentalOrderStatus.Approved), cancellationToken);
        await CompleteStudentAddressesAsync(admin, deliveryOrder.Id, "Test Sokak 1", cancellationToken);
        await PostAsync<OrderKitPreparationResponse>(admin, $"/api/orders/{deliveryOrder.Id}/kits",
            new { lines = new[] { new { productModelId = model.Id, quantity = 1 } }, useAvailableKits = true }, cancellationToken);
        await PostAsync<OrderResponse>(admin, $"/api/orders/{deliveryOrder.Id}/status-transitions",
            new OrderTransitionRequest(RentalOrderStatus.Preparing), cancellationToken);
        await PostAsync<OrderResponse>(admin, $"/api/orders/{deliveryOrder.Id}/status-transitions",
            new OrderTransitionRequest(RentalOrderStatus.OutboundInTransit), cancellationToken);

        var otherCustomer = CreateClient(new TokenUser(Guid.NewGuid(), "other@portal.test", "CustomerUser", Guid.NewGuid()));
        var forbiddenConfirmation = await otherCustomer.PostAsJsonAsync(
            $"/api/customer-portal/orders/{deliveryOrder.Id}/delivery-confirmations", new { }, cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, forbiddenConfirmation.StatusCode);

        var confirmation = await customer.PostAsJsonAsync(
            $"/api/customer-portal/orders/{deliveryOrder.Id}/delivery-confirmations", new { }, cancellationToken);
        confirmation.EnsureSuccessStatusCode();
        var confirmedOrder = (await confirmation.Content.ReadFromJsonAsync<OrderResponse>(cancellationToken))!;
        Assert.Equal(RentalOrderStatus.Completed, confirmedOrder.Status);

        overview = await customer.GetFromJsonAsync<CustomerPortalResponse>("/api/customer-portal", cancellationToken);
        Assert.Contains(overview!.Orders, item => item.Id == deliveryOrder.Id && item.Status == RentalOrderStatus.Completed);
        Assert.Contains(overview.Kits, item => item.ProductUnitId == deliverableUnit.Id &&
            item.UnitStatus == KitRental.Core.Domain.Inventory.ProductUnitStatus.WithCustomer);

        var adminOrders = (await admin.GetFromJsonAsync<PagedResponse<PortalOrderResponse>>("/api/order-summaries?pageSize=5000", cancellationToken))!.Items;
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

    private static async Task<string> CreatePublicTokenAsync(HttpClient client, string qrCode,
        CancellationToken cancellationToken) =>
        (await PostAsync<PublicFormAccessTokenResponse>(client,
            $"/api/public/form-access/{Uri.EscapeDataString(qrCode)}", new { }, cancellationToken)).Token;

    private static async Task CompleteStudentAddressesAsync(HttpClient client, Guid orderId, string addressLine,
        CancellationToken cancellationToken)
    {
        var detail = await client.GetFromJsonAsync<OrderDetailResponse>($"/api/orders/{orderId}",
            cancellationToken);
        foreach (var student in detail!.Students)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/public/student-addresses/{student.PublicAddressToken}",
                new PublicStudentAddressRequest(addressLine), cancellationToken);
            response.EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task PublicQrCreatesOpenFaultAndReturnRequestForActiveRental()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var today = TurkeyTime.Today();
        var admin = CreateClient(new TokenUser(Guid.NewGuid(), "admin-public-qr@test.local", "SystemAdmin", null));
        var publicClient = _factory.CreateClient();
        var model = await PostAsync<ProductModelResponse>(admin, "/api/product-models",
            new CreateProductModelRequest("Public QR Test Kiti", $"PQR-{Guid.NewGuid():N}"), cancellationToken);
        var unit = await PostAsync<ProductUnitResponse>(admin, "/api/product-units",
            new CreateProductUnitRequest(model.Id, $"PQR-SN-{Guid.NewGuid():N}", $"PQR-QR-{Guid.NewGuid():N}"), cancellationToken);
        var rental = await PostAsync<RentPhysicalKitResponse>(admin, $"/api/physical-kits/{unit.Id}/rentals",
            new RentPhysicalKitRequest("Public QR Musterisi", $"public-{Guid.NewGuid():N}@example.com",
                "05320000000", "Test Sokak 10", "34000",
                today.AddDays(-1), today.AddDays(30)), cancellationToken);

        var token = await CreatePublicTokenAsync(publicClient, unit.QrCode, cancellationToken);
        var fault = await publicClient.PostAsJsonAsync("/api/public/faults", new PublicFaultRequest(
            null, token, "Ayse Test", "05321112233", "Test Sokak 10 Kadikoy Istanbul",
            "Kit acildiginda sensor okumasi yapmiyor."), cancellationToken);
        fault.EnsureSuccessStatusCode();
        var createdFault = (await fault.Content.ReadFromJsonAsync<CreatedFaultResponse>(cancellationToken))!;
        var faultPage = await admin.GetFromJsonAsync<FaultPageResponse>(
            "/api/faults?page=1&pageSize=10&query=Ayse%20Test", cancellationToken);
        var listedFault = Assert.Single(faultPage!.Items, item => item.Id == createdFault.Id);
        Assert.Equal(FaultApprovalStatus.NotRequired, listedFault.ApprovalStatus);
        Assert.Equal(FaultStatus.Open, listedFault.Status);

        var createdReturn = await PostAsync<PublicReturnResponse>(publicClient, "/api/public/returns",
            new PublicKitReturnRequest(token, "Ayse Test", "05321112233", "Test Sokak 10 Kadikoy Istanbul", null, null, KitReturnReason.EducationCompleted),
            cancellationToken);
        Assert.Equal(KitReturnStatus.Requested, createdReturn.Status);

        var duplicateReturn = await publicClient.PostAsJsonAsync("/api/public/returns",
            new PublicKitReturnRequest(token, "Ayse Test", "05321112233", "Test Sokak 10 Kadikoy Istanbul", 41.012345, 29.012345, KitReturnReason.EducationCompleted),
            cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.Created, duplicateReturn.StatusCode);

        var dashboard = await admin.GetFromJsonAsync<DashboardResponse>("/api/dashboard", cancellationToken);
        var dashboardReturn = Assert.Single(dashboard!.ReturnsInProgress, item => item.Id == createdReturn.Id);
        Assert.Equal("Ayse Test", dashboardReturn.RequesterName);
        Assert.Equal("0532 111 22 33", dashboardReturn.RequesterPhone);
        Assert.Equal("Test Sokak 10 Kadikoy Istanbul", dashboardReturn.ReturnAddress);
        Assert.Equal(41.012345, dashboardReturn.Latitude);
        Assert.Equal(29.012345, dashboardReturn.Longitude);
        Assert.Equal(rental.AssignmentId, createdReturn.Items.Single().AssignmentId);

        var availableUnit = await PostAsync<ProductUnitResponse>(admin, "/api/product-units",
            new CreateProductUnitRequest(model.Id, $"PQR-FREE-{Guid.NewGuid():N}", $"PQR-FREE-QR-{Guid.NewGuid():N}"),
            cancellationToken);
        var unavailableToken = await CreatePublicTokenAsync(publicClient, availableUnit.QrCode, cancellationToken);
        var unavailableFault = await publicClient.PostAsJsonAsync("/api/public/faults", new PublicFaultRequest(
            null, unavailableToken, "Ayse Test", "05321112233", "Test Sokak 10 Kadikoy Istanbul",
            "Aktif kiralamasi olmayan kit."), cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, unavailableFault.StatusCode);
        var unavailableReturn = await publicClient.PostAsJsonAsync("/api/public/returns",
            new PublicKitReturnRequest(unavailableToken, "Ayse Test", "05321112233", "Test Sokak 10 Kadikoy Istanbul", 41.012345, 29.012345, KitReturnReason.EnrollmentCancelled),
            cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, unavailableReturn.StatusCode);
    }

    [Fact]
    public async Task PublicQrReceivesInTransitKitAndAddsDashboardLocation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var admin = CreateClient(new TokenUser(Guid.NewGuid(), "admin-public-delivery@test.local", "SystemAdmin", null));
        var publicClient = _factory.CreateClient();
        var model = await PostAsync<ProductModelResponse>(admin, "/api/product-models",
            new CreateProductModelRequest("Public Teslim Kiti", $"PDL-{Guid.NewGuid():N}"), cancellationToken);
        var unit = await PostAsync<ProductUnitResponse>(admin, "/api/product-units",
            new CreateProductUnitRequest(model.Id, $"PDL-SN-{Guid.NewGuid():N}", $"PDL-QR-{Guid.NewGuid():N}"), cancellationToken);
        var customer = await PostAsync<CustomerResponse>(admin, "/api/customers",
            new CreateCustomerRequest("Teslim Okulu", $"delivery-{Guid.NewGuid():N}@example.com",
                new AddressRequest("Okul", "Operasyon", "02120000000", "Okul Sokak 1", "06000")),
            cancellationToken);
        var order = await PostAsync<CreatedOrderResponse>(admin, "/api/orders", new CreateOrderRequest(
            customer.Id, model.Id, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1),
            [new CreateOrderStudentRequest("Ece Yilmaz", "05325550000")]), cancellationToken);

        await PostAsync<OrderResponse>(admin, $"/api/orders/{order.Id}/status-transitions",
            new OrderTransitionRequest(RentalOrderStatus.Approved), cancellationToken);
        await CompleteStudentAddressesAsync(admin, order.Id, "Okul Sokak 1", cancellationToken);
        var prepared = await PostAsync<OrderKitPreparationResponse>(admin, $"/api/orders/{order.Id}/kits",
            new { lines = new[] { new { productModelId = model.Id, quantity = 1 } }, useAvailableKits = true },
            cancellationToken);
        Assert.Equal(unit.Id, prepared.Kits.Single().ProductUnitId);
        await PostAsync<OrderResponse>(admin, $"/api/orders/{order.Id}/status-transitions",
            new OrderTransitionRequest(RentalOrderStatus.Preparing), cancellationToken);
        await PostAsync<OrderResponse>(admin, $"/api/orders/{order.Id}/status-transitions",
            new OrderTransitionRequest(RentalOrderStatus.OutboundInTransit), cancellationToken);

        var token = await CreatePublicTokenAsync(publicClient, unit.QrCode, cancellationToken);
        var receipt = await PostAsync<PublicDeliveryResponse>(publicClient, "/api/public/deliveries",
            new PublicKitDeliveryRequest(token, "Ece Yilmaz", "05325550000",
                "Ataturk Caddesi 12", 41.0438, 29.0094), cancellationToken);
        Assert.Equal(unit.Id, receipt.ProductUnitId);
        Assert.Equal(prepared.Kits.Single().AssignmentId, receipt.AssignmentId);

        var dashboard = await admin.GetFromJsonAsync<DashboardResponse>("/api/dashboard", cancellationToken);
        var location = Assert.Single(dashboard!.KitLocations, item => item.ProductUnitId == unit.Id);
        Assert.Equal("Ece Yilmaz", location.RecipientName);
        Assert.Equal(41.0438, location.Latitude);
        Assert.Equal(29.0094, location.Longitude);

        var detail = await admin.GetFromJsonAsync<PhysicalKitDetailResponse>(
            $"/api/physical-kits/{unit.Id}", cancellationToken);
        Assert.Equal("Ece Yilmaz", detail!.CurrentLocation!.RecipientName);
        Assert.Equal("Ataturk Caddesi 12", detail.CurrentLocation.AddressLine);
        var rentalHistory = Assert.Single(detail.RentalHistory);
        Assert.Equal("Ece Yilmaz", rentalHistory.RecipientName);
        Assert.Equal("Ataturk Caddesi 12", rentalHistory.AddressLine);
        Assert.Contains(detail!.StatusHistory, item =>
            item.NewStatus == ProductUnitStatus.WithCustomer &&
            item.Reason.Contains("Ece Yilmaz", StringComparison.OrdinalIgnoreCase));

        var units = (await admin.GetFromJsonAsync<PagedResponse<ProductUnitResponse>>("/api/product-units?pageSize=5000", cancellationToken))!.Items;
        Assert.Equal(ProductUnitStatus.WithCustomer, units!.Single(item => item.Id == unit.Id).Status);
    }

    [Fact]
    public async Task StudentOrderCompletionRequiresAddressAndWritesStudentKitLocation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var admin = CreateClient(new TokenUser(Guid.NewGuid(), "admin-student-location@test.local", "SystemAdmin", null));
        var publicClient = _factory.CreateClient();
        var model = await PostAsync<ProductModelResponse>(admin, "/api/product-models",
            new CreateProductModelRequest("Adres Sonra Kiti", $"ASK-{Guid.NewGuid():N}"), cancellationToken);
        var unit = await PostAsync<ProductUnitResponse>(admin, "/api/product-units",
            new CreateProductUnitRequest(model.Id, $"ASK-SN-{Guid.NewGuid():N}", $"ASK-QR-{Guid.NewGuid():N}"),
            cancellationToken);
        var customer = await PostAsync<CustomerResponse>(admin, "/api/customers",
            new CreateCustomerRequest("Adres Sonra Okulu", $"address-later-{Guid.NewGuid():N}@example.com",
                new AddressRequest("Okul", "Operasyon", "02120000000", "Okul Sokak 1", "06000")),
            cancellationToken);
        var order = await PostAsync<CreatedOrderResponse>(admin, "/api/orders", new CreateOrderRequest(
            customer.Id, model.Id, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1),
            [new CreateOrderStudentRequest("Adres Bekleyen Öğrenci", "05320000000")]), cancellationToken);

        await PostAsync<OrderResponse>(admin, $"/api/orders/{order.Id}/status-transitions",
            new OrderTransitionRequest(RentalOrderStatus.Approved), cancellationToken);
        var orderDetail = await admin.GetFromJsonAsync<OrderDetailResponse>(
            $"/api/orders/{order.Id}", cancellationToken);
        var student = Assert.Single(orderDetail!.Students);
        Assert.False(student.HasAddress);

        var prepared = await PostAsync<OrderKitPreparationResponse>(admin, $"/api/orders/{order.Id}/kits",
            new { lines = Array.Empty<OrderLineRequest>(), useAvailableKits = true },
            cancellationToken);
        Assert.Equal(unit.Id, prepared.Kits.Single().ProductUnitId);

        var blockedCompletion = await admin.PostAsJsonAsync($"/api/orders/{order.Id}/status-transitions",
            new OrderTransitionRequest(RentalOrderStatus.Completed), cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, blockedCompletion.StatusCode);

        var detail = await admin.GetFromJsonAsync<PhysicalKitDetailResponse>(
            $"/api/physical-kits/{unit.Id}", cancellationToken);
        Assert.NotEqual("Adres Bekleyen Öğrenci", detail!.CurrentLocation?.RecipientName);
        Assert.DoesNotContain(detail.DeliveryHistory, item => item.RecipientName == "Adres Bekleyen Öğrenci");

        var addressResponse = await publicClient.PostAsJsonAsync(
            $"/api/public/student-addresses/{student.PublicAddressToken}",
            new PublicStudentAddressRequest("İstanbul / Kadıköy - Test Sokak 1", 99, null),
            cancellationToken);
        addressResponse.EnsureSuccessStatusCode();

        detail = await admin.GetFromJsonAsync<PhysicalKitDetailResponse>(
            $"/api/physical-kits/{unit.Id}", cancellationToken);
        Assert.NotEqual("Adres Bekleyen Öğrenci", detail!.CurrentLocation?.RecipientName);
        Assert.DoesNotContain(detail.DeliveryHistory, item => item.RecipientName == "Adres Bekleyen Öğrenci");

        var completedOrder = await PostAsync<OrderResponse>(admin, $"/api/orders/{order.Id}/status-transitions",
            new OrderTransitionRequest(RentalOrderStatus.Completed), cancellationToken);
        Assert.Equal(RentalOrderStatus.Completed, completedOrder.Status);

        detail = await admin.GetFromJsonAsync<PhysicalKitDetailResponse>(
            $"/api/physical-kits/{unit.Id}", cancellationToken);
        Assert.Equal("Adres Bekleyen Öğrenci", detail!.CurrentLocation!.RecipientName);
        Assert.Equal("İstanbul / Kadıköy - Test Sokak 1", detail.CurrentLocation.AddressLine);
        Assert.Null(detail.CurrentLocation.Latitude);
        Assert.Null(detail.CurrentLocation.Longitude);
    }

    [Fact]
    public async Task CustomerPortalUnassignedKitCountExcludesReturnedKits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var today = TurkeyTime.Today();
        var admin = CreateClient(new TokenUser(Guid.NewGuid(), "admin-active-kit@test.local", "SystemAdmin", null));
        var publicClient = _factory.CreateClient();
        var model = await PostAsync<ProductModelResponse>(admin, "/api/product-models",
            new CreateProductModelRequest("Aktif Kit Sayim Testi", $"AKT-{Guid.NewGuid():N}"), cancellationToken);
        var faultyUnit = await PostAsync<ProductUnitResponse>(admin, "/api/product-units",
            new CreateProductUnitRequest(model.Id, $"AKT-FLT-{Guid.NewGuid():N}", $"AKT-FLT-QR-{Guid.NewGuid():N}"),
            cancellationToken);
        var returnedUnit = await PostAsync<ProductUnitResponse>(admin, "/api/product-units",
            new CreateProductUnitRequest(model.Id, $"AKT-RET-{Guid.NewGuid():N}", $"AKT-RET-QR-{Guid.NewGuid():N}"),
            cancellationToken);

        var email = $"active-kit-{Guid.NewGuid():N}@example.com";
        var faultyRental = await PostAsync<RentPhysicalKitResponse>(admin, $"/api/physical-kits/{faultyUnit.Id}/rentals",
            new RentPhysicalKitRequest("Aktif Kit Musterisi", email, "05320000000", "Test Sokak 1",
                "34000", today.AddDays(-5), today.AddDays(10)), cancellationToken);
        var returnedRental = await PostAsync<RentPhysicalKitResponse>(admin, $"/api/physical-kits/{returnedUnit.Id}/rentals",
            new RentPhysicalKitRequest("Aktif Kit Musterisi", email, "05320000000", "Test Sokak 2",
                "34000", today.AddDays(-5), today.AddDays(10)), cancellationToken);

        var customer = CreateClient(new TokenUser(Guid.NewGuid(), email, "CustomerAccountManager", faultyRental.CustomerId));
        var initialOverview = await customer.GetFromJsonAsync<CustomerPortalResponse>("/api/customer-portal", cancellationToken);
        Assert.Equal(0, initialOverview!.ActiveKitCount);
        Assert.Equal(2, initialOverview.UnassignedKitCount);

        var faultResponse = await customer.PostAsJsonAsync("/api/customer-portal/faults", new PortalFaultRequest(
            faultyRental.AssignmentId, "Aktif Kit Musterisi", "05320000000", "Test Sokak 2",
            "Kit calisirken sensor verisi gelmiyor."), cancellationToken);
        faultResponse.EnsureSuccessStatusCode();

        var returnedToken = await CreatePublicTokenAsync(publicClient, returnedUnit.QrCode, cancellationToken);
        var publicReturn = await PostAsync<PublicReturnResponse>(publicClient, "/api/public/returns",
            new PublicKitReturnRequest(returnedToken, "Aktif Kit Musterisi", "05320000000",
                "Test Sokak 2 Kadikoy Istanbul", null, null, KitReturnReason.EnrollmentCancelled), cancellationToken);
        await PostAsync<ReturnResponse>(admin, $"/api/kit-returns/{publicReturn.Id}/receipts", new { }, cancellationToken);

        var updatedOverview = await customer.GetFromJsonAsync<CustomerPortalResponse>("/api/customer-portal", cancellationToken);
        Assert.Equal(0, updatedOverview!.ActiveKitCount);
        Assert.Equal(1, updatedOverview.UnassignedKitCount);
        var faultyLocation = Assert.Single(updatedOverview.KitLocations);
        Assert.Equal(faultyUnit.Id, faultyLocation.ProductUnitId);
        Assert.Equal("faulty", faultyLocation.LocationCategory);
        Assert.DoesNotContain(updatedOverview.KitLocations, item => item.ProductUnitId == returnedUnit.Id);

        var dashboard = await admin.GetFromJsonAsync<DashboardResponse>("/api/dashboard", cancellationToken);
        var dashboardFaultyLocation = Assert.Single(dashboard!.KitLocations, item => item.ProductUnitId == faultyUnit.Id);
        Assert.Equal("faulty", dashboardFaultyLocation.LocationCategory);
        Assert.DoesNotContain(dashboard.KitLocations, item => item.ProductUnitId == returnedUnit.Id);
    }

    [Fact]
    public async Task AdminManagesFaultGuideEntriesAndPublicReadsActiveOnes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var admin = CreateClient(new TokenUser(Guid.NewGuid(), "admin-guide@test.local", "SystemAdmin", null));
        var publicClient = _factory.CreateClient();
        var firstModel = await PostAsync<ProductModelResponse>(admin, "/api/product-models",
            new CreateProductModelRequest("Rehber Kiti A", $"GUIDE-A-{Guid.NewGuid():N}"), cancellationToken);
        var firstUnit = await PostAsync<ProductUnitResponse>(admin, "/api/product-units",
            new CreateProductUnitRequest(firstModel.Id, QrCode: $"GUIDE-QR-A-{Guid.NewGuid():N}"), cancellationToken);
        var secondModel = await PostAsync<ProductModelResponse>(admin, "/api/product-models",
            new CreateProductModelRequest("Rehber Kiti B", $"GUIDE-B-{Guid.NewGuid():N}"), cancellationToken);

        var created = await PostAsync<FaultGuideEntryResponse>(admin, "/api/fault-guides",
            new FaultGuideEntryRequest("Sensor okumuyor", "Sensor degeri surekli sifir gorunuyor.",
                "Kablo yonunu ve port secimini kontrol edin.", 10, true, firstModel.Id), cancellationToken);
        var secondGuide = await PostAsync<FaultGuideEntryResponse>(admin, "/api/fault-guides",
            new FaultGuideEntryRequest("B kitinde motor duruyor", "B kitine özel sorun.",
                "B kitinin motor bağlantısını kontrol edin.", 15, true, secondModel.Id), cancellationToken);
        var passive = await PostAsync<FaultGuideEntryResponse>(admin, "/api/fault-guides",
            new FaultGuideEntryRequest("Pasif rehber", "Public ekranda gorunmemeli.",
                "Admin tarafinda sakli kalir.", 20, false), cancellationToken);

        var publicEntries = (await publicClient.GetFromJsonAsync<PagedResponse<FaultGuideEntryResponse>>(
            "/api/public/fault-guides?pageSize=5000", cancellationToken))!.Items;
        Assert.Contains(publicEntries!, item => item.Id == created.Id);
        Assert.DoesNotContain(publicEntries!, item => item.Id == passive.Id);
        var firstToken = await CreatePublicTokenAsync(publicClient, firstUnit.QrCode, cancellationToken);
        var kitEntries = (await publicClient.GetFromJsonAsync<PagedResponse<FaultGuideEntryResponse>>(
            $"/api/public/fault-guides/{firstToken}?pageSize=5000", cancellationToken))!.Items;
        Assert.Contains(kitEntries!, item => item.Id == created.Id);
        Assert.DoesNotContain(kitEntries!, item => item.Id == secondGuide.Id);

        var delete = await admin.DeleteAsync($"/api/fault-guides/{created.Id}", cancellationToken);
        delete.EnsureSuccessStatusCode();
        publicEntries = (await publicClient.GetFromJsonAsync<PagedResponse<FaultGuideEntryResponse>>(
            "/api/public/fault-guides?pageSize=5000", cancellationToken))!.Items;
        Assert.DoesNotContain(publicEntries!, item => item.Id == created.Id);
    }

    [Fact]
    public async Task CustomerCanReturnExpiredSelectedKitAndAdminReceivesItIntoAvailableStock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var today = TurkeyTime.Today();
        var admin = CreateClient(new TokenUser(Guid.NewGuid(), "admin@return.test", "SystemAdmin", null));
        var model = await PostAsync<ProductModelResponse>(admin, "/api/product-models",
            new CreateProductModelRequest("İade Test Kiti", $"RET-{Guid.NewGuid():N}"), cancellationToken);
        var unit = await PostAsync<ProductUnitResponse>(admin, "/api/product-units",
            new CreateProductUnitRequest(model.Id, $"RET-SN-{Guid.NewGuid():N}", $"RET-QR-{Guid.NewGuid():N}"), cancellationToken);
        var expiringUnit = await PostAsync<ProductUnitResponse>(admin, "/api/product-units",
            new CreateProductUnitRequest(model.Id, $"EXP-SN-{Guid.NewGuid():N}", $"EXP-QR-{Guid.NewGuid():N}"), cancellationToken);
        var email = $"return-{Guid.NewGuid():N}@example.com";
        var rental = await PostAsync<RentPhysicalKitResponse>(admin, $"/api/physical-kits/{unit.Id}/rentals",
            new RentPhysicalKitRequest("İade Müşterisi", email, "02120000000", "Test Sokak 1",
                "34000", today.AddMonths(-2), today.AddDays(-1)), cancellationToken);
        var customer = CreateClient(new TokenUser(Guid.NewGuid(), email, "CustomerAccountManager", rental.CustomerId));
        await PostAsync<RentPhysicalKitResponse>(admin, $"/api/physical-kits/{expiringUnit.Id}/rentals",
            new RentPhysicalKitRequest("Yaklaşan Kiralama", $"expiring-{Guid.NewGuid():N}@example.com", "02120000001",
                "Test Sokak 2", "34000", today.AddDays(-10), today.AddDays(7)), cancellationToken);

        var expiryDashboard = await admin.GetFromJsonAsync<DashboardResponse>("/api/dashboard", cancellationToken);
        Assert.Contains(expiryDashboard!.ExpiredRentalKits, x => x.ProductUnitId == unit.Id && x.DaysRemaining < 0);
        Assert.Contains(expiryDashboard.ExpiringRentalKits,
            x => x.ProductUnitId == expiringUnit.Id && x.DaysRemaining is >= 0 and <= 7);

        var created = await PostAsync<ReturnResponse>(customer, "/api/customer-portal/returns",
            new { assignmentIds = new[] { rental.AssignmentId } }, cancellationToken);
        Assert.Equal(KitReturnStatus.Requested, created.Status);
        var shipped = await PostAsync<ReturnResponse>(customer, $"/api/customer-portal/returns/{created.Id}/shipments",
            new { carrier = "Test Kargo", trackingNumber = $"TK-{Guid.NewGuid():N}" }, cancellationToken);
        Assert.Equal(KitReturnStatus.InTransit, shipped.Status);

        var dashboard = await admin.GetFromJsonAsync<DashboardResponse>("/api/dashboard", cancellationToken);
        Assert.Contains(dashboard!.ReturnsInProgress, x => x.Id == created.Id && x.KitCount == 1);
        await PostAsync<ReturnResponse>(admin, $"/api/kit-returns/{created.Id}/receipts", new { }, cancellationToken);

        var units = (await admin.GetFromJsonAsync<PagedResponse<ProductUnitResponse>>("/api/product-units?pageSize=5000", cancellationToken))!.Items;
        Assert.Equal(ProductUnitStatus.Available, units!.Single(x => x.Id == unit.Id).Status);
        var overview = await customer.GetFromJsonAsync<CustomerPortalResponse>("/api/customer-portal", cancellationToken);
        Assert.Contains(overview!.Returns, x => x.Id == created.Id && x.Status == KitReturnStatus.Received);
    }

    [Fact]
    public async Task CustomerRentalCohortLocksApprovedStudentListAndUnlinksStudentKitAfterReturnReceived()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var admin = CreateClient(new TokenUser(Guid.NewGuid(), "admin-cohort@test.local", "SystemAdmin", null));
        var model = await PostAsync<ProductModelResponse>(admin, "/api/product-models",
            new CreateProductModelRequest("TACEV Öğrenci Kiti", $"TCK-{Guid.NewGuid():N}"), cancellationToken);
        var customer = await PostAsync<CustomerResponse>(admin, "/api/customers",
            new CreateCustomerRequest("TACEV Cohort", $"cohort-{Guid.NewGuid():N}@example.com",
                new AddressRequest("Merkez", "TACEV", "02120000000", "Bilim Sokak 1", "34000")),
            cancellationToken);
        var portal = CreateClient(new TokenUser(Guid.NewGuid(), "tacev-cohort@test.local",
            "CustomerAccountManager", customer.Id));
        var cohort = await PostAsync<PortalRentalCohortResponse>(portal, "/api/customer-portal/rental-periods",
            new RentalCohortRequest("2026 Güz", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 31)),
            cancellationToken);
        var student = await PostAsync<PortalRentalCohortStudentResponse>(portal,
            $"/api/customer-portal/rental-periods/{cohort.Id}/students",
            new RentalCohortStudentRequest("Ayşe Yılmaz", "05320000000", "Test Mahallesi 1", model.Id),
            cancellationToken);
        var orders = (await portal.GetFromJsonAsync<PagedResponse<PortalOrderResponse>>("/api/orders?pageSize=5000", cancellationToken))!.Items;
        var order = Assert.Single(orders!, item => item.Status == RentalOrderStatus.PendingApproval);
        var editableStudent = await PostAsync<PortalRentalCohortStudentResponse>(portal,
            $"/api/customer-portal/rental-periods/{cohort.Id}/students",
            new RentalCohortStudentRequest("Mehmet Yılmaz", "05320000001", "Test Mahallesi 2", model.Id),
            cancellationToken);
        await portal.DeleteAsync($"/api/customer-portal/rental-periods/{cohort.Id}/students/{editableStudent.Id}",
            cancellationToken);
        await PostAsync<OrderResponse>(admin, $"/api/orders/{order.Id}/status-transitions",
            new OrderTransitionRequest(RentalOrderStatus.Approved), cancellationToken);
        var orderDetail = await admin.GetFromJsonAsync<OrderDetailResponse>(
            $"/api/orders/{order.Id}", cancellationToken);
        Assert.Equal(cohort.Id, orderDetail!.RentalCohortId);

        var prepared = await PostAsync<OrderKitPreparationResponse>(admin, $"/api/orders/{order.Id}/kits",
            new { lines = Array.Empty<OrderLineRequest>(), useAvailableKits = true },
            cancellationToken);

        var overview = await portal.GetFromJsonAsync<CustomerPortalResponse>("/api/customer-portal", cancellationToken);
        var assigned = overview!.RentalCohorts.Single(x => x.Id == cohort.Id).Students.Single(x => x.Id == student.Id);
        Assert.Equal(prepared.Kits.Single().ProductUnitId, assigned.ProductUnitId);
        Assert.Equal(prepared.Kits.Single().AssignmentId, assigned.AssignmentId);
        Assert.False(assigned.HasDeliveryForm);
        Assert.False(overview.Kits.Single(x => x.AssignmentId == assigned.AssignmentId).HasDeliveryForm);

        var detail = await admin.GetFromJsonAsync<PhysicalKitDetailResponse>(
            $"/api/physical-kits/{prepared.Kits.Single().ProductUnitId}", cancellationToken);
        Assert.Contains(detail!.ActivityHistory, item =>
            item.Description.Contains("Ayşe Yılmaz", StringComparison.OrdinalIgnoreCase) &&
            item.Action.Contains("atandı", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(detail.DeliveryHistory, item => item.RecipientName == "Ayşe Yılmaz");

        var blockedAdd = await portal.PostAsJsonAsync(
            $"/api/customer-portal/rental-periods/{cohort.Id}/students",
            new RentalCohortStudentRequest("Mehmet Yılmaz", "05320000001", "Test Mahallesi 2", model.Id),
            cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, blockedAdd.StatusCode);

        var blockedDelete = await portal.DeleteAsync(
            $"/api/customer-portal/rental-periods/{cohort.Id}/students/{student.Id}", cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, blockedDelete.StatusCode);

        await PostAsync<OrderResponse>(admin, $"/api/orders/{order.Id}/status-transitions",
            new OrderTransitionRequest(RentalOrderStatus.Completed), cancellationToken);

        overview = await portal.GetFromJsonAsync<CustomerPortalResponse>("/api/customer-portal", cancellationToken);
        assigned = overview!.RentalCohorts.Single(x => x.Id == cohort.Id).Students.Single(x => x.Id == student.Id);
        Assert.True(assigned.HasDeliveryForm);
        Assert.Equal("Ayşe Yılmaz", assigned.DeliveredTo);
        Assert.Equal("0532 000 00 00", assigned.DeliveryPhone);
        Assert.Equal("Test Mahallesi 1", assigned.DeliveryAddress);
        Assert.True(overview.Kits.Single(x => x.AssignmentId == assigned.AssignmentId).HasDeliveryForm);

        var returnRequest = await PostAsync<ReturnResponse>(portal,
            $"/api/customer-portal/rental-periods/{cohort.Id}/students/{student.Id}/returns",
            new PortalStudentReturnRequest("Ayşe Yılmaz", "05320000000", "Test Mahallesi 1",
                KitReturnReason.EducationCompleted),
            cancellationToken);
        Assert.Equal(KitReturnStatus.Requested, returnRequest.Status);
        await PostAsync<ReturnResponse>(admin, $"/api/kit-returns/{returnRequest.Id}/receipts", new { },
            cancellationToken);

        overview = await portal.GetFromJsonAsync<CustomerPortalResponse>("/api/customer-portal", cancellationToken);
        var updatedCohort = overview!.RentalCohorts.Single(x => x.Id == cohort.Id);
        var returnedStudent = Assert.Single(updatedCohort.Students);
        Assert.Equal("Ayşe Yılmaz", returnedStudent.FullName);
        Assert.Equal("0532 000 00 00", returnedStudent.GuardianPhone);
        Assert.Equal(prepared.Kits.Single().AssignmentId, returnedStudent.AssignmentId);
        Assert.Equal(prepared.Kits.Single().ProductUnitId, returnedStudent.ProductUnitId);
        Assert.True(returnedStudent.HasCompletedReturn);
        Assert.Empty(updatedCohort.UnassignedKits);

        var units = (await admin.GetFromJsonAsync<PagedResponse<ProductUnitResponse>>("/api/product-units?pageSize=5000", cancellationToken))!.Items;
        Assert.Equal(ProductUnitStatus.Available, units!.Single(x => x.Id == prepared.Kits.Single().ProductUnitId).Status);

        detail = await admin.GetFromJsonAsync<PhysicalKitDetailResponse>(
            $"/api/physical-kits/{prepared.Kits.Single().ProductUnitId}", cancellationToken);
        var receivedLocation = Assert.Single(detail!.DeliveryHistory);
        Assert.Equal("Robotik Bilim Atölye", receivedLocation.RecipientName);
        Assert.Equal("Robotik Bilim Atölye", receivedLocation.AddressLine);
        Assert.Null(receivedLocation.Latitude);
        Assert.Null(receivedLocation.Longitude);
        Assert.Contains(detail!.ActivityHistory, item =>
            item.Description.Contains("kit ilişkisi geçmiş kayıt olarak korundu", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record CreatedFaultResponse(Guid Id);
    private sealed record CreatedOrderResponse(Guid Id, OrderType Type);
    private sealed record OrderResponse(Guid Id, RentalOrderStatus Status);
    private sealed record CustomerResponse(Guid Id, IReadOnlyCollection<AddressResponse> Addresses);
    private sealed record AddressResponse(Guid Id);
    private sealed record PublicDeliveryResponse(Guid Id, Guid ProductUnitId, Guid AssignmentId);
    private sealed record ReturnResponse(Guid Id, KitReturnStatus Status);
    private sealed record PublicReturnResponse(Guid Id, KitReturnStatus Status,
        IReadOnlyCollection<PublicReturnItemResponse> Items);
    private sealed record PublicReturnItemResponse(Guid AssignmentId, Guid ProductUnitId, Guid OrderId);
}



