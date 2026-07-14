using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using KitRental.Core.Application.Procurement;
using KitRental.Core.Application.Workshop;
using KitRental.Core.Domain.Procurement;
using KitRental.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KitRental.Core.IntegrationTests;

public sealed class SupplyNeedApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SupplyNeedApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing")).CreateClient();
        var tokens = new TokenService(new TokenOptions("KitRental.Identity", "KitRental",
            "development-only-secret-change-before-production-2026", TimeSpan.FromHours(8)));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            tokens.Create(new TokenUser(Guid.NewGuid(), "supply-needs@test.local", "SystemAdmin", null),
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task SupplyNeed_CanBeCreatedUpdatedCompletedAndAddedToStock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var component = await PostAsync<ComponentResponse>("/api/components",
            new CreateComponentRequest("Tedarik Test Komponenti", $"SUP-{Guid.NewGuid():N}", "adet", 0),
            cancellationToken);
        var location = await PostAsync<StorageLocationResponse>("/api/storage-locations",
            new CreateStorageLocationRequest($"SUP-{Guid.NewGuid():N}", "Test Depo", "A", "1", "1"),
            cancellationToken);

        var created = await PostAsync<SupplyNeedResponse>("/api/supply-needs",
            new SupplyNeedRequest([new SupplyNeedLineRequest(component.Id, 12)]), cancellationToken);
        Assert.Equal(SupplyNeedStatus.Pending, created.Status);
        Assert.Equal(12, created.Lines.Single().Quantity);
        Assert.NotEqual(default, created.CreatedAt);

        var updatedResponse = await _client.PutAsJsonAsync($"/api/supply-needs/{created.Id}",
            new SupplyNeedRequest([new SupplyNeedLineRequest(component.Id, 20)]), cancellationToken);
        updatedResponse.EnsureSuccessStatusCode();
        var updated = await updatedResponse.Content.ReadFromJsonAsync<SupplyNeedResponse>(cancellationToken);
        Assert.Equal(20, updated!.Lines.Single().Quantity);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);

        var statusResponse = await _client.PostAsJsonAsync($"/api/supply-needs/{created.Id}/complete",
            new CompleteSupplyNeedRequest(location.Id, [new SupplyNeedLineRequest(component.Id, 18)]), cancellationToken);
        statusResponse.EnsureSuccessStatusCode();
        var supplied = await statusResponse.Content.ReadFromJsonAsync<SupplyNeedResponse>(cancellationToken);
        Assert.Equal(SupplyNeedStatus.Supplied, supplied!.Status);
        Assert.Equal(18, supplied.Lines.Single().SuppliedQuantity);

        var stocks = await _client.GetFromJsonAsync<ComponentStockResponse[]>(
            $"/api/component-stock?componentId={component.Id}&locationId={location.Id}", cancellationToken);
        Assert.Equal(18, stocks!.Single().Quantity);

        var repeatedResponse = await _client.PostAsJsonAsync($"/api/supply-needs/{created.Id}/complete",
            new CompleteSupplyNeedRequest(location.Id, [new SupplyNeedLineRequest(component.Id, 18)]), cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, repeatedResponse.StatusCode);

        var deleteResponse = await _client.DeleteAsync($"/api/supply-needs/{created.Id}", cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken cancellationToken)
    {
        var response = await _client.PostAsJsonAsync(path, body, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(cancellationToken))!;
    }
}
