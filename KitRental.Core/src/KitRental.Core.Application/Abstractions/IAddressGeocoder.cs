namespace KitRental.Core.Application.Abstractions;

public sealed record GeocodedAddress(double Latitude, double Longitude, string? District, string? City);

public interface IAddressGeocoder
{
    Task<GeocodedAddress?> GeocodeAsync(string addressLine, string? district, string? city,
        CancellationToken cancellationToken);
}
