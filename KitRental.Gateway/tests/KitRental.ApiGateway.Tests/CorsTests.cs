using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace KitRental.ApiGateway.Tests;

public sealed class CorsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CorsTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task BearerPreflightDoesNotRequireUpstreamServices()
    {
        using var client = _factory.CreateClient();
        using var request = Preflight("https://standalone.example.com");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("*", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        Assert.Contains("authorization", string.Join(',', response.Headers.GetValues("Access-Control-Allow-Headers")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExplicitOriginsDoNotAuthorizeUnknownWebsites()
    {
        await using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "https://ui.example.com"
            })));
        using var client = factory.CreateClient();
        using var allowedRequest = Preflight("https://ui.example.com");
        using var allowed = await client.SendAsync(allowedRequest, TestContext.Current.CancellationToken);
        Assert.Equal("https://ui.example.com", Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Origin")));
        using var deniedRequest = Preflight("https://other.example.com");
        using var denied = await client.SendAsync(deniedRequest, TestContext.Current.CancellationToken);
        Assert.False(denied.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/core/api/orders");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return request;
    }
}
