using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace KitRental.Identity.IntegrationTests;

public sealed class SwaggerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public SwaggerTests(WebApplicationFactory<Program> factory) =>
        _client = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing")).CreateClient();

    [Fact]
    public async Task SwaggerDocument_IsPubliclyAvailable()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
