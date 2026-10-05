using System.Net;
using System.Net.Http.Json;
using KitRental.Web.Mvc.Models;
using KitRental.Web.Mvc.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace KitRental.Web.Tests;

public sealed class AddressRegionPageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AddressRegionPageTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Local")
            .ConfigureTestServices(services => services.AddHttpClient<KitRentalApiClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new RegionApiHandler())));
    }

    [Fact]
    public async Task PublicAddressPageRendersSeparateRegionIdsAndStreetWithoutStaticCatalog()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/adres/test-token", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Contains("name=\"CityId\"", html);
        Assert.Contains("name=\"DistrictId\"", html);
        Assert.Contains("data-city-id=\"34\"", html);
        Assert.Contains("data-district-id=\"340\"", html);
        Assert.Contains("A Blok / 2. Kat - Bilim Sokak 1", html);
        Assert.Contains("/js/address-regions.js", html);
        Assert.DoesNotContain("turkey-address-dropdowns.js", html);
    }

    [Theory]
    [InlineData("/address-regions/cities", 34, "İstanbul")]
    [InlineData("/address-regions/districts?cityId=34", 340, "Kadıköy")]
    public async Task RegionProxyExposesProviderIdsAndNames(string path, int id, string name)
    {
        using var client = _factory.CreateClient();
        var regions = await client.GetFromJsonAsync<AddressRegionViewModel[]>(path, TestContext.Current.CancellationToken);

        var region = Assert.Single(regions!);
        Assert.Equal(id, region.Id);
        Assert.Equal(name, region.Name);
    }

    [Fact]
    public async Task RegionProxyRejectsMissingProvince()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/address-regions/districts", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class RegionApiHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            object data = request.RequestUri!.AbsolutePath switch
            {
                "/core/api/address-regions/cities" => new[] { new AddressRegionViewModel(34, "İstanbul") },
                "/core/api/address-regions/districts/34" => new[] { new AddressRegionViewModel(340, "Kadıköy") },
                _ => new PublicStudentAddressContextViewModel("Test Öğrencisi", "05320000000", "Test Kurumu",
                    "TEST-1", "Test Kiti", "A Blok / 2. Kat - Bilim Sokak 1", null, null,
                    34, 340, "İstanbul", "Kadıköy")
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(data) });
        }
    }
}
