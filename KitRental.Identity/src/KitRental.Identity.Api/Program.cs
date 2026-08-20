using KitRental.Identity.Api.Extensions;
using KitRental.Identity.Api.Middleware;
using KitRental.Identity.Infrastructure;
using KitRental.Observability;
using KitRental.Security;
using Microsoft.OpenApi;

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
        Title = "KitRental Identity API",
        Version = "v1",
        Description = "Kullanıcı, rol, oturum ve erişim belirteci işlemleri."
    });
    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Login yanıtındaki erişim belirtecini girin."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("bearer", document)] = []
    });
});
builder.Services.AddKitRentalObservability();
builder.Services.AddKitRentalSecurity(tokenOptions);
builder.Services.AddIdentityServices(builder.Configuration, useInMemoryPersistence);

var app = builder.Build();
if (!useInMemoryPersistence)
    await app.Services.InitializeMongoIdentityAsync();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "KitRental Identity API v1");
    options.DocumentTitle = "KitRental Identity API";
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
