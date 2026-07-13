using KitRental.Identity.Application;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace KitRental.Identity.Infrastructure;

public static class MongoPersistenceExtensions
{
    public static IServiceCollection AddMongoIdentityPersistence(
        this IServiceCollection services,
        string connectionString,
        string databaseName)
    {
        services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
        services.AddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName));
        services.AddSingleton<MongoUserRepository>();
        services.AddSingleton<IUserRepository>(serviceProvider => serviceProvider.GetRequiredService<MongoUserRepository>());
        services.AddSingleton<IIdentityStoreInitializer>(serviceProvider => serviceProvider.GetRequiredService<MongoUserRepository>());
        return services;
    }

    public static async Task InitializeMongoIdentityAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        var initializer = services.GetRequiredService<IIdentityStoreInitializer>();
        await initializer.InitializeAsync(cancellationToken);
    }
}
