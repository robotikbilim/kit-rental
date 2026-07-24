using KitRental.Identity.Application;
using KitRental.Identity.Domain;
using KitRental.Security;

namespace KitRental.Identity.Infrastructure;

public sealed class InMemoryUserRepository : IUserRepository
{
    public static readonly Guid DevelopmentAdminId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly object _gate = new();
    private readonly Dictionary<Guid, UserAccount> _users;

    public InMemoryUserRepository(IPasswordHasher passwordHasher)
    {
        var admin = UserAccount.Create(
            DevelopmentAdminId,
            "admin@robotikbilim.com.tr",
            "Sistem Yöneticisi",
            passwordHasher.Hash("41yaD3r!n58"),
            UserRole.SystemAdmin,
            null);
        _users = new Dictionary<Guid, UserAccount> { [admin.Id] = admin };
    }

    public Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_users.Values.SingleOrDefault(user => user.Email == email));
        }
    }

    public Task<IReadOnlyCollection<UserAccount>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyCollection<UserAccount>>(_users.Values.ToArray());
        }
    }

    public Task AddAsync(UserAccount user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_users.Values.Any(existing => existing.Email == user.Email))
            {
                throw new InvalidOperationException("E-posta adresi benzersiz olmalıdır.");
            }

            _users.Add(user.Id, user);
        }

        return Task.CompletedTask;
    }
}
