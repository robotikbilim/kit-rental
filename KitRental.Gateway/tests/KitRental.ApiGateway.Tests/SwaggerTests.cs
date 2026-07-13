using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KitRental.ApiGateway.Tests;

public sealed class SwaggerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SwaggerTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task SwaggerDocument_ContainsGatewayRoutes()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<SwaggerDocument>(TestContext.Current.CancellationToken);
        Assert.Equal("KitRental API Gateway", document!.Info.Title);
        Assert.Contains("/health", document.Paths.Keys);
        Assert.Contains("/core/{path}", document.Paths.Keys);
        Assert.Contains("/identity/{path}", document.Paths.Keys);

        var swaggerUiConfiguration = await _client.GetStringAsync(
            "/swagger/index.js",
            TestContext.Current.CancellationToken);
        Assert.Contains("/identity/swagger/v1/swagger.json", swaggerUiConfiguration, StringComparison.Ordinal);
        Assert.Contains("/core/swagger/v1/swagger.json", swaggerUiConfiguration, StringComparison.Ordinal);
    }

    private sealed record SwaggerDocument(SwaggerInfo Info, Dictionary<string, object> Paths);
    private sealed record SwaggerInfo(string Title);
}
