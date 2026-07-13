using KitRental.Core.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KitRental.Core.Infrastructure.Persistence;

public static class CorePersistenceExtensions
{
    public static IServiceCollection AddSqlServerPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<KitRentalDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sql => sql.MigrationsAssembly(typeof(KitRentalDbContext).Assembly.FullName)
                    .EnableRetryOnFailure(3)));
        services.AddScoped<ICoreRepository, EfCoreRepository>();
        return services;
    }

    public static async Task MigrateCoreDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KitRentalDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
