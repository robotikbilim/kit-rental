using Microsoft.Extensions.Options;

namespace KitRental.Web.Mvc.Branding;

public sealed class BrandDefinition
{
    public string Name { get; init; } = "Robotik Bilim";
    public string ShortTitle { get; init; } = "RB Atölye";
    public string? LogoPath { get; init; }
    public string ThemeClass { get; init; } = "brand-robotik-bilim";
    public string Eyebrow { get; init; } = "OPERASYON PORTALI";
    public string LoginHeading { get; init; } = "Eğitim kiti kiralama süreçlerini tek yerden yönetin.";
    public string LoginDescription { get; init; } = "Operasyon ekibi için güvenli kiralama ve servis portalı.";
}

public sealed class BrandingOptions
{
    public BrandDefinition Default { get; init; } = new();
    public Dictionary<string, BrandDefinition> Hosts { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public interface IBrandResolver
{
    BrandDefinition Current { get; }
}

public sealed class HostBrandResolver(
    IHttpContextAccessor httpContextAccessor,
    IOptions<BrandingOptions> options) : IBrandResolver
{
    private readonly Lazy<BrandDefinition> _current = new(() =>
    {
        var host = NormalizeHost(httpContextAccessor.HttpContext?.Request.Host.Host);
        if (host is not null && options.Value.Hosts.TryGetValue(host, out var brand))
            return brand;

        return options.Value.Default;
    });

    public BrandDefinition Current => _current.Value;

    private static string? NormalizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return null;

        return host.Trim().TrimEnd('.').ToLowerInvariant();
    }
}
