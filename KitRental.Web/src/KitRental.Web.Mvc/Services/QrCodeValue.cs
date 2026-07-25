namespace KitRental.Web.Mvc.Services;

public static class QrCodeValue
{
    public static string Normalize(string? rawValue)
    {
        var value = rawValue?.Trim() ?? string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return value;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var routeIndex = Array.FindIndex(segments,
            segment => segment.Equals("ariza", StringComparison.OrdinalIgnoreCase));
        return routeIndex >= 0 && routeIndex + 1 < segments.Length
            ? Uri.UnescapeDataString(segments[routeIndex + 1])
            : value;
    }
}
