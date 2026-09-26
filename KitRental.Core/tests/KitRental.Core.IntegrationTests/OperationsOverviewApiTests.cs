using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using KitRental.Core.Application.Operations;
using KitRental.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KitRental.Core.IntegrationTests;

public sealed class OperationsOverviewApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public OperationsOverviewApiTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));

    [Fact]
    public async Task AdminCanBindFiltersAndReadAnEmptyCustomerScope()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateClient("SystemAdmin");
        var customerId = Guid.NewGuid();
        var page = await client.GetFromJsonAsync<OperationsOrderPage>(
            $"/api/operations/orders?customerId={customerId}&query=test&type=1&status=3&focus=overdue&endsFrom=2026-09-01&endsTo=2026-10-01&sort=end-date&page=99&pageSize=10",
            cancellationToken);
        Assert.NotNull(page);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(10, page.PageSize);
        Assert.Empty(page.Items);
        var dashboard = await client.GetFromJsonAsync<OperationsDashboardResponse>(
            $"/api/dashboard?customerId={customerId}", cancellationToken);
        Assert.NotNull(dashboard);
        Assert.Equal(customerId, dashboard.CustomerId);
        Assert.Equal(0, dashboard.TotalOrders);
        Assert.Empty(dashboard.PriorityOrders);
    }

    [Theory]
    [InlineData("/api/operations/orders")]
    [InlineData("/api/dashboard")]
    [InlineData("/api/faults/11111111-1111-1111-1111-111111111111")]
    public async Task CustomerCannotAccessAdminOperationalProjections(string path)
    {
        using var client = CreateClient("CustomerAdmin", Guid.NewGuid());
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("SystemAdmin")]
    [InlineData("OperationsManager")]
    [InlineData("WarehouseStaff")]
    [InlineData("ServiceTechnician")]
    [InlineData("Auditor")]
    public async Task FaultDetailReturnsNotFoundForMissingRecord(string role)
    {
        using var client = CreateClient(role);
        var response = await client.GetAsync($"/api/faults/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateClient(string role, Guid? customerId = null)
    {
        var client = _factory.CreateClient();
        var tokens = new TokenService(new TokenOptions("KitRental.Identity", "KitRental",
            "development-only-secret-change-before-production-2026", TimeSpan.FromHours(8)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            tokens.Create(new TokenUser(Guid.NewGuid(), "operations@test.local", role, customerId), DateTimeOffset.UtcNow));
        return client;
    }
}
