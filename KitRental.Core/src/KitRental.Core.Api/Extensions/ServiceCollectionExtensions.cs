using KitRental.Core.Application.Abstractions;
using KitRental.Core.Application.CustomerPortal;
using KitRental.Core.Application.Inventory;
using KitRental.Core.Application.Operations;
using KitRental.Core.Application.PhysicalKits;
using KitRental.Core.Application.Procurement;
using KitRental.Core.Application.Rentals;
using KitRental.Core.Application.Reporting;
using KitRental.Core.Application.Workshop;
using KitRental.Core.Infrastructure.Persistence;

namespace KitRental.Core.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreServices(
        this IServiceCollection services,
        IConfiguration configuration,
        bool useInMemoryPersistence)
    {
        if (useInMemoryPersistence)
        {
            services.AddSingleton<ICoreRepository, InMemoryCoreRepository>();
        }
        else
        {
            var connectionString = configuration.GetConnectionString("CoreDatabase")
                ?? throw new InvalidOperationException("CoreDatabase bağlantı dizesi tanımlanmalıdır.");
            services.AddSqlServerPersistence(connectionString);
        }

        services.AddScoped<InventoryService>();
        services.AddScoped<ProductUnitStockConsumptionPlanner>();
        services.AddScoped<RentalAssignmentService>();
        services.AddScoped<OperationsService>();
        services.AddScoped<ReportingService>();
        services.AddScoped<WorkshopService>();
        services.AddScoped<PhysicalKitService>();
        services.AddScoped<CustomerPortalService>();
        services.AddScoped<SupplyNeedService>();
        services.AddSingleton<IEmailNotificationQueue, EmailNotificationQueue>();
        services.AddHostedService<EmailNotificationWorker>();
        services.AddScoped<EmailNotificationDispatcher>();
        services.AddScoped<IEmailNotificationService, QueuedEmailNotificationService>();

        services.AddHttpClient<IAddressGeocoder, NominatimAddressGeocoder>(client =>
            client.DefaultRequestHeaders.UserAgent.ParseAdd(configuration["Geocoding:UserAgent"]
                ?? "KitRental/1.0 (admin@robotikbilim.com.tr)"));
        services.AddHttpClient("identity-notifications", client =>
            client.BaseAddress = new Uri(configuration["Notifications:IdentityBaseUrl"]
                ?? "https://localhost:59592"));

        return services;
    }
}
