using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using KitRental.Core.Application.Abstractions;

namespace KitRental.Core.Api;

public sealed class NominatimAddressGeocoder(HttpClient httpClient, ILogger<NominatimAddressGeocoder> logger)
    : IAddressGeocoder
{
    public async Task<GeocodedAddress?> GeocodeAsync(string addressLine, string? district, string? city,
        CancellationToken cancellationToken)
    {
        var query = BuildQuery(addressLine, district, city);
        if (string.IsNullOrWhiteSpace(query)) return null;

        try
        {
            var url = new UriBuilder("https://nominatim.openstreetmap.org/search")
            {
                Query = string.Join("&",
                [
                    "format=jsonv2",
                    $"q={Uri.EscapeDataString(query)}",
                    "limit=1",
                    "countrycodes=tr",
                    "addressdetails=1",
                    "accept-language=tr"
                ])
            }.Uri;
            var results = await httpClient.GetFromJsonAsync<IReadOnlyCollection<NominatimSearchResult>>(
                url, cancellationToken);
            var first = results?.FirstOrDefault();
            if (first is null ||
                !double.TryParse(first.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
                !double.TryParse(first.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude) ||
                !CoordinatesAreValid(latitude, longitude))
                return null;

            var resultCity = first.Address?.Province ?? first.Address?.City ?? first.Address?.Town ?? first.Address?.State;
            var resultDistrict = first.Address?.County ?? first.Address?.CityDistrict ?? first.Address?.Town ?? first.Address?.Suburb;
            return new GeocodedAddress(latitude, longitude, resultDistrict, resultCity);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Address geocoding failed for '{AddressLine}'.", addressLine);
            return null;
        }
    }

    private static string BuildQuery(string addressLine, string? district, string? city)
    {
        var parts = new[] { addressLine, district, city, "Turkiye" }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim());
        return string.Join(", ", parts);
    }

    private static bool CoordinatesAreValid(double latitude, double longitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    private sealed record NominatimSearchResult(
        [property: JsonPropertyName("lat")] string? Lat,
        [property: JsonPropertyName("lon")] string? Lon,
        [property: JsonPropertyName("address")] NominatimAddress? Address);

    private sealed record NominatimAddress(
        [property: JsonPropertyName("province")] string? Province,
        [property: JsonPropertyName("city")] string? City,
        [property: JsonPropertyName("town")] string? Town,
        [property: JsonPropertyName("state")] string? State,
        [property: JsonPropertyName("county")] string? County,
        [property: JsonPropertyName("city_district")] string? CityDistrict,
        [property: JsonPropertyName("suburb")] string? Suburb);
}
