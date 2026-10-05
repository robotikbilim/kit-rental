namespace KitRental.Web.Mvc.Services;

public static class AddressDisplay
{
    public static string Full(string? street, string? city, string? district) =>
        string.Join(", ", new[] { street, district, city }
            .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));

    public static string Region(string? city, string? district) =>
        string.Join(" · ", new[]
        {
            string.IsNullOrWhiteSpace(district) ? null : $"İlçe: {district.Trim()}",
            string.IsNullOrWhiteSpace(city) ? null : $"İl: {city.Trim()}"
        }.Where(value => value is not null));
}
