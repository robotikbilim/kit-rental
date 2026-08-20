namespace KitRental.Core.Domain.Locations;

public sealed class LocationCity
{
    private LocationCity() { }

    public LocationCity(int id, string code, string name)
    {
        Id = id;
        Code = code;
        Name = name;
    }

    public int Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
}
