using System.Security.Claims;

namespace KitRental.Security;

public interface ITokenService
{
    string Create(TokenUser user, DateTimeOffset now);
    ClaimsPrincipal? Validate(string token, DateTimeOffset now);
}
