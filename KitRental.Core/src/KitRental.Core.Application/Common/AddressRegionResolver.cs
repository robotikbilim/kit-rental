using KitRental.Core.Application.Kargonomi;
using KitRental.Core.Domain.Customers;
using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Rentals;
using KitRental.Core.Domain.Returns;
using KitRental.Core.Domain.Support;
using KitRental.SharedKernel;
using System.Globalization;
using System.Text;

namespace KitRental.Core.Application.Common;

public sealed record AddressRegion(int? CityId, int? DistrictId, string? City, string? District);

public static class AddressRegionResolver
{
    public static async Task<AddressRegion> ResolveNamesAsync(IKargonomiClient? client, string? city,
        string? district, CancellationToken cancellationToken, bool required = true)
    {
        if (string.IsNullOrWhiteSpace(city) && string.IsNullOrWhiteSpace(district) && !required)
            return new(null, null, null, null);
        if (string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(district))
            throw new ConflictException("address.region_required", "Adres için il ve ilçe birlikte girilmelidir.");
        if (client is null)
            throw new ConflictException("address.catalog_unavailable", "İl ve ilçe Kargonomi adres listesinden doğrulanamadı.");

        var normalizedCity = NormalizeName(city);
        var states = await client.GetStatesAsync(cancellationToken);
        var stateMatches = states.Where(item => NormalizeName(item.Name) == normalizedCity).ToArray();
        if (stateMatches.Length != 1)
            throw new ConflictException("address.city_invalid", "Girilen il Kargonomi adres listesinde bulunamadı.");

        var state = stateMatches[0];
        var normalizedDistrict = NormalizeName(district);
        var districts = await client.GetCitiesAsync(state.Id, cancellationToken);
        var districtMatches = districts.Where(item => NormalizeName(item.Name) == normalizedDistrict).ToArray();
        if (districtMatches.Length != 1)
            throw new ConflictException("address.district_invalid", "Girilen ilçe seçilen ile ait Kargonomi listesinde bulunamadı.");

        var selectedDistrict = districtMatches[0];
        return new(state.Id, selectedDistrict.Id, state.Name, selectedDistrict.Name);
    }

    public static async Task<AddressRegion> ResolveAsync(IKargonomiClient? client, int? cityId,
        int? districtId, string? city, string? district, CancellationToken cancellationToken,
        bool required = true)
    {
        if (!required && cityId is null && districtId is null)
            return new(null, null, null, null);
        if (cityId is not > 0 || districtId is not > 0)
            throw new ConflictException("address.region_required", "Lütfen il ve ilçe seçin.");
        // Direct service tests may provide a catalog-free dependency. Production DI always supplies the client.
        if (client is null)
        {
            if (string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(district))
                throw new ConflictException("address.region_required", "Lütfen il ve ilçe seçin.");
            return new(cityId, districtId, city.Trim(), district.Trim());
        }
        var state = (await client.GetStatesAsync(cancellationToken)).SingleOrDefault(item => item.Id == cityId);
        if (state is null)
            throw new ConflictException("address.city_invalid", "Seçilen il Kargonomi adres listesinde bulunamadı.");
        var selectedDistrict = (await client.GetCitiesAsync(state.Id, cancellationToken))
            .SingleOrDefault(item => item.Id == districtId);
        if (selectedDistrict is null)
            throw new ConflictException("address.district_invalid", "Seçilen ilçe bu ile ait değil.");
        return new(state.Id, selectedDistrict.Id, state.Name, selectedDistrict.Name);
    }

    private static string NormalizeName(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;
            normalized.Append(char.ToLowerInvariant(character switch
            {
                'ı' => 'i',
                _ => character
            }));
        }
        return normalized.ToString().Normalize(NormalizationForm.FormC);
    }
}

public sealed record StoredAddress(string? Street, int? CityId, int? DistrictId, string? City, string? District)
{
    public static StoredAddress From(Address? value) => new(value?.Line1, value?.CityId, value?.DistrictId, value?.City, value?.District);
    public static StoredAddress From(AddressSnapshot? value) => new(value?.Line1, value?.CityId, value?.DistrictId, value?.City, value?.District);
    public static StoredAddress From(KitLocationEvent? value) => new(value?.AddressLine, value?.CityId, value?.DistrictId, value?.City, value?.District);
    public static StoredAddress From(RentalCohortStudent? value) => new(value?.AddressLine, value?.CityId, value?.DistrictId, value?.City, value?.District);
    public static StoredAddress From(FaultTicket? value) => new(value?.ReporterAddress, value?.CityId, value?.DistrictId, value?.City, value?.District);
    public static StoredAddress From(KitReturnRequest? value) => new(value?.ReturnAddress, value?.CityId, value?.DistrictId, value?.City, value?.District);
    public static StoredAddress First(params StoredAddress[] values) => values.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Street)) ?? new(null, null, null, null, null);
}
