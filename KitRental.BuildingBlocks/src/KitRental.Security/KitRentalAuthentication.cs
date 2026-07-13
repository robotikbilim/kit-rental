using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KitRental.Security;

public static class KitRentalAuthenticationDefaults
{
    public const string Scheme = "KitRentalBearer";
}

public sealed class KitRentalAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ITokenService tokenService,
    TimeProvider timeProvider)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var principal = tokenService.Validate(authorization[7..].Trim(), timeProvider.GetUtcNow());
        return Task.FromResult(principal is null
            ? AuthenticateResult.Fail("Geçersiz veya süresi dolmuş erişim belirteci.")
            : AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}

public static class SecurityServiceCollectionExtensions
{
    public static IServiceCollection AddKitRentalSecurity(this IServiceCollection services, TokenOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton(TimeProvider.System);
        services.AddAuthentication(KitRentalAuthenticationDefaults.Scheme)
            .AddScheme<AuthenticationSchemeOptions, KitRentalAuthenticationHandler>(
                KitRentalAuthenticationDefaults.Scheme,
                _ => { });
        services.AddAuthorization();
        return services;
    }

    public static Guid GetRequiredUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Kullanıcı kimliği bulunamadı."));

    public static Guid? GetCustomerId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue("customer_id"), out var id) ? id : null;
}
