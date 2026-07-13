using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Customers;

public sealed record Address(
    Guid Id,
    string Title,
    string ContactName,
    string Phone,
    string Line1,
    string District,
    string City,
    string PostalCode);

public sealed record AddressSnapshot(
    string ContactName,
    string Phone,
    string Line1,
    string District,
    string City,
    string PostalCode);

public sealed class Customer
{
    private readonly List<Address> _addresses = [];

    private Customer()
    {
    }

    private Customer(Guid id, string name, string email)
    {
        Id = id;
        Name = name;
        Email = email;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public IReadOnlyCollection<Address> Addresses => _addresses.AsReadOnly();

    public static Customer Create(Guid id, string name, string email)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("customer.required_fields", "Müşteri adı ve e-posta zorunludur.");
        }

        if (!email.Contains('@', StringComparison.Ordinal))
        {
            throw new DomainException("customer.invalid_email", "Geçerli bir müşteri e-postası girilmelidir.");
        }

        return new Customer(id, name.Trim(), email.Trim().ToLowerInvariant());
    }

    public Address AddAddress(
        string title,
        string contactName,
        string phone,
        string line1,
        string district,
        string city,
        string postalCode)
    {
        if (new[] { title, contactName, phone, line1, district, city }.Any(string.IsNullOrWhiteSpace))
        {
            throw new DomainException("address.required_fields", "Adres başlığı, iletişim ve konum alanları zorunludur.");
        }

        var address = new Address(
            Guid.NewGuid(), title.Trim(), contactName.Trim(), phone.Trim(), line1.Trim(), district.Trim(), city.Trim(), postalCode.Trim());
        _addresses.Add(address);
        return address;
    }

    public AddressSnapshot SnapshotAddress(Guid addressId)
    {
        var address = _addresses.SingleOrDefault(item => item.Id == addressId)
            ?? throw new DomainException("address.not_found", "Müşteri adresi bulunamadı.");
        return new AddressSnapshot(
            address.ContactName,
            address.Phone,
            address.Line1,
            address.District,
            address.City,
            address.PostalCode);
    }
}
