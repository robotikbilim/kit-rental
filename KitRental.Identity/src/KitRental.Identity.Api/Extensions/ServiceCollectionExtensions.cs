using KitRental.Identity.Application;
using KitRental.Identity.Infrastructure;

namespace KitRental.Identity.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityServices(
        this IServiceCollection services,
        IConfiguration configuration,
        bool useInMemoryPersistence)
    {
        if (useInMemoryPersistence)
        {
            services.AddSingleton<IUserRepository, InMemoryUserRepository>();
        }
        else
        {
            var mongoConnection = configuration["Mongo:ConnectionString"]
                ?? throw new InvalidOperationException("Mongo bağlantı dizesi tanımlanmalıdır.");
            var mongoDatabase = configuration["Mongo:Database"]
                ?? throw new InvalidOperationException("Mongo veritabanı adı tanımlanmalıdır.");
            services.AddMongoIdentityPersistence(mongoConnection, mongoDatabase);
        }

        services.AddScoped<IdentityService>();
        return services;
    }
}
