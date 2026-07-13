using KitRental.Identity.Domain;

namespace KitRental.Identity.Application;

public interface IUserRepository
{
    Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<UserAccount>> GetAllAsync(CancellationToken cancellationToken);
    Task AddAsync(UserAccount user, CancellationToken cancellationToken);
}
