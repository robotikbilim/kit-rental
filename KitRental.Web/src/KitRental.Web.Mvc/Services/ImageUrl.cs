namespace KitRental.Web.Mvc.Services;

public static class ImageUrl
{
    private const string KitFallback = "/images/catalog/kit.svg";

    public static string Kit(string? value) => Resolve(value, KitFallback);

    private static string Resolve(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;

        var trimmed = value.Trim();
        if (trimmed.EndsWith("kit-placeholder.svg", StringComparison.OrdinalIgnoreCase)) return fallback;
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out _)) return trimmed;
        if (trimmed.StartsWith("~/", StringComparison.Ordinal)) return trimmed[1..];
        if (trimmed.StartsWith('/')) return trimmed;
        return "/" + trimmed;
    }
}
