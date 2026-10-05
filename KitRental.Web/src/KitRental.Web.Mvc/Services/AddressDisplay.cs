namespace KitRental.Web.Mvc.Services;

public static class AddressDisplay
{
    public static string Full(string? street, string? city, string? district) =>
        string.Join(", ", new[] { street, district, city }
            .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));
}
