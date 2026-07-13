using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KitRental.Observability;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddKitRentalObservability(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddHealthChecks();
        return services;
    }

    public static IApplicationBuilder UseKitRentalObservability(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var correlationId = context.Request.Headers.TryGetValue("X-Correlation-ID", out var existing)
                ? existing.ToString()
                : Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
            context.Response.Headers["X-Correlation-ID"] = correlationId;
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Request");
            using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
            {
                await next(context);
            }
        });
}
