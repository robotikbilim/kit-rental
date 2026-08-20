namespace KitRental.Core.Domain.Locations;

public sealed class LocationDistrict
{
    private LocationDistrict() { }

    public LocationDistrict(int id, int cityId, string name)
    {
        Id = id;
        CityId = cityId;
        Name = name;
    }

    public int Id { get; private set; }
    public int CityId { get; private set; }
    public string Name { get; private set; } = string.Empty;
}
