using System.Security.Claims;
using Microsoft.OpenApi;
using KitRental.Identity.Application;
using KitRental.Identity.Domain;
using KitRental.Identity.Infrastructure;
using KitRental.Observability;
using KitRental.Security;
using KitRental.SharedKernel;

var builder = WebApplication.CreateBuilder(args);
var tokenOptions = new TokenOptions(
    "KitRental.Identity",
    "KitRental",
    builder.Configuration["Security:TokenSecret"] ?? "development-only-secret-change-before-production-2026",
    TimeSpan.FromHours(8));

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
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
var useInMemoryPersistence = builder.Environment.IsEnvironment("Testing") ||
    builder.Configuration.GetValue<bool>("Persistence:UseInMemory");
if (useInMemoryPersistence)
{
    builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();
}
else
{
    var mongoConnection = builder.Configuration["Mongo:ConnectionString"]
        ?? throw new InvalidOperationException("Mongo bağlantı dizesi tanımlanmalıdır.");
    var mongoDatabase = builder.Configuration["Mongo:Database"]
        ?? throw new InvalidOperationException("Mongo veritabanı adı tanımlanmalıdır.");
    builder.Services.AddMongoIdentityPersistence(mongoConnection, mongoDatabase);
}
builder.Services.AddScoped<IdentityService>();

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
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var status = exception is DomainException ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError;
    context.Response.StatusCode = status;
    await Results.Problem(
        statusCode: status,
        title: status == 400 ? "Kimlik işlemi başarısız" : "Beklenmeyen hata",
        detail: exception?.Message,
        extensions: new Dictionary<string, object?>
        {
            ["code"] = exception is DomainException domain ? domain.Code : "server.error"
        }).ExecuteAsync(context);
}));
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api");
api.MapPost("/auth/login", async (LoginRequest request, IdentityService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.LoginAsync(new LoginCommand(request.Email, request.Password), cancellationToken)));

api.MapGet("/auth/me", (ClaimsPrincipal user) => Results.Ok(new
{
    id = user.GetRequiredUserId(),
    email = user.FindFirstValue(ClaimTypes.Email),
    role = user.FindFirstValue(ClaimTypes.Role),
    customerId = user.GetCustomerId()
})).RequireAuthorization();

api.MapGet("/users", async (IdentityService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetUsersAsync(cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(
        UserRole.SystemAdmin.ToString(), UserRole.OperationsManager.ToString()));

api.MapPost("/users", async (CreateUserRequest request, IdentityService service, CancellationToken cancellationToken) =>
{
    var result = await service.CreateUserAsync(
        new CreateUserCommand(request.Email, request.DisplayName, request.Password, request.Role, request.CustomerId),
        cancellationToken);
    return Results.Created($"/api/users/{result.Id}", result);
}).RequireAuthorization(policy => policy.RequireRole(
    UserRole.SystemAdmin.ToString(), UserRole.OperationsManager.ToString()));

app.MapHealthChecks("/health");
app.Run();

public sealed record LoginRequest(string Email, string Password);
public sealed record CreateUserRequest(string Email, string DisplayName, string Password, UserRole Role, Guid? CustomerId);
public partial class Program;
