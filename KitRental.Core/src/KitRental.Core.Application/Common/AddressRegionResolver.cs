using KitRental.Core.Application.Kargonomi;
using KitRental.Core.Domain.Customers;
using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Rentals;
using KitRental.Core.Domain.Returns;
using KitRental.Core.Domain.Support;
using KitRental.SharedKernel;

namespace KitRental.Core.Application.Common;

public sealed record AddressRegion(int? CityId, int? DistrictId, string? City, string? District);

public static class AddressRegionResolver
{
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
