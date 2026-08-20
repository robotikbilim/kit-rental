namespace KitRental.Identity.Infrastructure;

public interface IIdentityStoreInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}
