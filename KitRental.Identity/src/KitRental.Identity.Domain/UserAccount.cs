using KitRental.SharedKernel;

namespace KitRental.Identity.Domain;

public enum UserRole
{
    SystemAdmin = 1,
    OperationsManager = 2,
    WarehouseStaff = 3,
    ServiceTechnician = 4,
    CustomerAccountManager = 5,
    CustomerUser = 6,
    Auditor = 7
}

public sealed class UserAccount
{
    private UserAccount()
    {
    }

    private UserAccount(
        Guid id,
        string email,
        string displayName,
        string passwordHash,
        UserRole role,
        Guid? customerId)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        Role = role;
        CustomerId = customerId;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public Guid? CustomerId { get; private set; }
    public bool IsActive { get; private set; }

    public static UserAccount Create(
        Guid id,
        string email,
        string displayName,
        string passwordHash,
        UserRole role,
        Guid? customerId)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainException("user.required_fields", "Kullanıcı kimliği, e-posta ve görünen ad zorunludur.");
        }

        if (!email.Contains('@', StringComparison.Ordinal))
        {
            throw new DomainException("user.invalid_email", "Geçerli bir e-posta adresi girilmelidir.");
        }

        var customerRole = role is UserRole.CustomerAccountManager or UserRole.CustomerUser;
        if (customerRole != customerId.HasValue)
        {
            throw new DomainException("user.customer_scope_required", "Müşteri rolleri müşteri hesabına bağlı olmalıdır.");
        }

        return new UserAccount(id, email.Trim().ToLowerInvariant(), displayName.Trim(), passwordHash, role, customerId);
    }

    public void Deactivate() => IsActive = false;
}
