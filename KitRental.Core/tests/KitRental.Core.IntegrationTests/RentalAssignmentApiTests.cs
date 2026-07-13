using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using KitRental.Core.Application.Inventory;
using KitRental.Core.Application.Rentals;
using KitRental.Core.Domain.Orders;
using KitRental.Security;

namespace KitRental.Core.IntegrationTests;

public sealed class RentalAssignmentApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public RentalAssignmentApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing")).CreateClient();
        var tokenService = new TokenService(new TokenOptions(
            "KitRental.Identity", "KitRental", "development-only-secret-change-before-production-2026", TimeSpan.FromHours(8)));
        var token = tokenService.Create(
            new TokenUser(Guid.NewGuid(), "test@kitrental.local", "SystemAdmin", null),
            DateTimeOffset.UtcNow);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task CreateAssignment_RejectsOverlappingReservation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var modelResponse = await _client.PostAsJsonAsync(
            "/api/product-models",
            new CreateProductModelRequest("Başlangıç Robotik Seti", $"RB-{Guid.NewGuid():N}"),
            cancellationToken);
        modelResponse.EnsureSuccessStatusCode();
        var model = await modelResponse.Content.ReadFromJsonAsync<ProductModelResponse>(cancellationToken);

        var unitResponse = await _client.PostAsJsonAsync(
            "/api/product-units",
            new CreateProductUnitRequest(model!.Id, $"SN-{Guid.NewGuid():N}", $"QR-{Guid.NewGuid():N}"),
            cancellationToken);
        unitResponse.EnsureSuccessStatusCode();
        var unit = await unitResponse.Content.ReadFromJsonAsync<ProductUnitResponse>(cancellationToken);

        var customerResponse = await _client.PostAsJsonAsync(
            "/api/customers",
            new CreateCustomerRequest(
                "Test Okulu", $"test-{Guid.NewGuid():N}@example.com",
                new AddressRequest("Merkez", "Test Kullanıcısı", "5551112233", "Teknoloji Cad. 1", "Çankaya", "Ankara", "06500")),
            cancellationToken);
        customerResponse.EnsureSuccessStatusCode();
        var customer = await customerResponse.Content.ReadFromJsonAsync<CustomerApiResponse>(cancellationToken);

        var orderResponse = await _client.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderRequest(
                customer!.Id,
                customer.Addresses.Single().Id,
                new DateOnly(2026, 8, 1),
                new DateOnly(2026, 8, 10),
                [new OrderLineRequest(model.Id, 1)]),
            cancellationToken);
        orderResponse.EnsureSuccessStatusCode();
        var order = await orderResponse.Content.ReadFromJsonAsync<OrderApiResponse>(cancellationToken);

        var approveResponse = await _client.PostAsJsonAsync(
            $"/api/orders/{order!.Id}/transitions",
            new OrderTransitionRequest(RentalOrderStatus.Approved),
            cancellationToken);
        approveResponse.EnsureSuccessStatusCode();

        var firstResponse = await _client.PostAsJsonAsync(
            "/api/rental-assignments",
            CreateAssignmentRequest(order.Lines.Single().Id, customer.Id, unit!.Id, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 10)),
            cancellationToken);
        firstResponse.EnsureSuccessStatusCode();
        var firstAssignment = await firstResponse.Content.ReadFromJsonAsync<RentalAssignmentResponse>(cancellationToken);

        var overlappingResponse = await _client.PostAsJsonAsync(
            "/api/rental-assignments",
            CreateAssignmentRequest(order.Lines.Single().Id, customer.Id, unit.Id, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 10)),
            cancellationToken);

        Assert.NotNull(firstAssignment);
        Assert.Equal(HttpStatusCode.Conflict, overlappingResponse.StatusCode);
        var problem = await overlappingResponse.Content.ReadFromJsonAsync<ProblemDetailsResponse>(cancellationToken);
        Assert.Equal("rental_assignment.period_overlap", problem!.Code);
    }

    private static CreateRentalAssignmentRequest CreateAssignmentRequest(
        Guid orderLineId,
        Guid customerId,
        Guid unitId,
        DateOnly startDate,
        DateOnly endDate) =>
        new(orderLineId, customerId, unitId, startDate, endDate);

    private sealed record ProblemDetailsResponse(string Code);
    private sealed record CustomerApiResponse(Guid Id, IReadOnlyCollection<AddressApiResponse> Addresses);
    private sealed record AddressApiResponse(Guid Id);
    private sealed record OrderApiResponse(Guid Id, IReadOnlyCollection<OrderLineApiResponse> Lines);
    private sealed record OrderLineApiResponse(Guid Id);
}
