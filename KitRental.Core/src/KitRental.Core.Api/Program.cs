using KitRental.Core.Api.Extensions;
using KitRental.Core.Api.Middleware;
using KitRental.Core.Infrastructure.Persistence;
using KitRental.Observability;
using KitRental.Security;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
var tokenOptions = new TokenOptions(
    "KitRental.Identity",
    "KitRental",
    builder.Configuration["Security:TokenSecret"] ?? "development-only-secret-change-before-production-2026",
    TimeSpan.FromHours(8));
var useInMemoryPersistence = builder.Environment.IsEnvironment("Testing") ||
    builder.Configuration.GetValue<bool>("Persistence:UseInMemory");

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "KitRental Core API",
        Version = "v1",
        Description = "Müşteri, eğitim kiti, komponent stoğu, reçete, üretim, kiralama, kargo, arıza ve iade operasyonları."
    });
    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Identity API'den alınan erişim belirtecini girin."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("bearer", document)] = []
    });
});
builder.Services.AddKitRentalObservability();
builder.Services.AddKitRentalSecurity(tokenOptions);
builder.Services.AddCoreServices(builder.Configuration, useInMemoryPersistence);

var app = builder.Build();
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownIPNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
    if (IPAddress.TryParse(proxy, out var address)) forwardedOptions.KnownProxies.Add(address);
app.UseForwardedHeaders(forwardedOptions);
if (!useInMemoryPersistence)
    await app.Services.MigrateCoreDatabaseAsync();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "KitRental Core API v1");
    options.DocumentTitle = "KitRental Core API";
    options.DisplayRequestDuration();
    options.EnablePersistAuthorization();
});
app.UseKitRentalObservability();
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

public partial class Program;
