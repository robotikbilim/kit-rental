using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;

namespace KitRental.Web.Tests;

public sealed class BrandingPageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BrandingPageTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("atolye.et-edu.net", "Robotik Bilim", "brand-robotik-bilim", "RB Atölye")]
    [InlineData("tacev.et-edu.net", "TACEV", "brand-tacev", "TACEV Kit Portalı")]
    [InlineData("unknown.example", "Robotik Bilim", "brand-robotik-bilim", "RB Atölye")]
    public async Task Login_page_uses_host_brand(
        string host,
        string brandName,
        string themeClass,
        string title)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/account/login");
        request.Headers.Host = host;

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        response.EnsureSuccessStatusCode();
        Assert.Contains($"class=\"{themeClass}\"", html);
        Assert.Contains(brandName, html);
        Assert.Contains($"Giriş - {title}", html);
    }

    [Fact]
    public void Authentication_cookie_is_host_only()
    {
        var options = _factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        Assert.Null(options.Cookie.Domain);
        Assert.Equal("KitRental.Session", options.Cookie.Name);
    }
}
