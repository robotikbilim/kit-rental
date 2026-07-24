using KitRental.Web.Mvc.Branding;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace KitRental.Web.Tests;

public sealed class BrandResolverTests
{
    private static readonly BrandDefinition RobotikBilim = new()
    {
        Name = "Robotik Bilim",
        ThemeClass = "brand-robotik-bilim"
    };

    private static readonly BrandDefinition Tacev = new()
    {
        Name = "TACEV",
        ThemeClass = "brand-tacev"
    };

    [Theory]
    [InlineData("tacev.et-edu.net", "TACEV")]
    [InlineData("TACEV.ET-EDU.NET", "TACEV")]
    [InlineData("tacev.et-edu.net.", "TACEV")]
    [InlineData("atolye.et-edu.net", "Robotik Bilim")]
    [InlineData("localhost", "Robotik Bilim")]
    [InlineData("unknown.example", "Robotik Bilim")]
    public void Resolves_brand_from_normalized_host(string host, string expectedName)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host, 8443);
        var accessor = new HttpContextAccessor { HttpContext = context };
        var options = Options.Create(new BrandingOptions
        {
            Default = RobotikBilim,
            Hosts = new Dictionary<string, BrandDefinition>
            {
                ["atolye.et-edu.net"] = RobotikBilim,
                ["tacev.et-edu.net"] = Tacev
            }
        });

        var resolver = new HostBrandResolver(accessor, options);

        Assert.Equal(expectedName, resolver.Current.Name);
    }
}
