using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KitRental.Identity.IntegrationTests;

public sealed class AuthenticationApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public AuthenticationApiTests(WebApplicationFactory<Program> factory) =>
        _client = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing")).CreateClient();

    [Fact]
    public async Task Login_ReturnsToken_ThatCanAccessCurrentUser()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var login = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin@robotikbilim.com.tr", "41yaD3r!n58"),
            cancellationToken);
        login.EnsureSuccessStatusCode();
        var payload = await login.Content.ReadFromJsonAsync<LoginResult>(cancellationToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload!.AccessToken);
        var me = await _client.GetAsync("/api/auth/me", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task CurrentUser_RejectsAnonymousRequest()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SystemAdmin_CanCreateAnotherSystemAdmin()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var login = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin@robotikbilim.com.tr", "41yaD3r!n58"),
            cancellationToken);
        var payload = await login.Content.ReadFromJsonAsync<LoginResult>(cancellationToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload!.AccessToken);

        var response = await _client.PostAsJsonAsync("/api/users", new
        {
            email = $"admin-{Guid.NewGuid():N}@robotikbilim.com.tr",
            displayName = "Yeni Sistem Yöneticisi",
            password = "StrongPassword123!",
            role = 1,
            customerId = (Guid?)null
        }, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private sealed record LoginResult(string AccessToken);
}
