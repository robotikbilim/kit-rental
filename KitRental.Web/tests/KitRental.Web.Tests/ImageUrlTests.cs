using KitRental.Web.Mvc.Services;

namespace KitRental.Web.Tests;

public sealed class ImageUrlTests
{
    [Theory]
    [InlineData(null, "/images/catalog/kit.svg")]
    [InlineData("", "/images/catalog/kit.svg")]
    [InlineData("images/robotluk/red-kit.png", "/images/robotluk/red-kit.png")]
    [InlineData("/images/robotluk/red-kit.png", "/images/robotluk/red-kit.png")]
    [InlineData("~/images/robotluk/red-kit.png", "/images/robotluk/red-kit.png")]
    [InlineData("images/kit-placeholder.svg", "/images/catalog/kit.svg")]
    [InlineData("https://example.com/kit.png", "https://example.com/kit.png")]
    public void Kit_resolves_image_paths_for_browser_src(string? value, string expected) =>
        Assert.Equal(expected, ImageUrl.Kit(value));
}
