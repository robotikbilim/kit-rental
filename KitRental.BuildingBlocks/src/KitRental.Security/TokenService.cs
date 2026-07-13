using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KitRental.Security;

public sealed record TokenOptions(string Issuer, string Audience, string Secret, TimeSpan Lifetime);

public sealed record TokenUser(Guid Id, string Email, string Role, Guid? CustomerId);

public interface ITokenService
{
    string Create(TokenUser user, DateTimeOffset now);
    ClaimsPrincipal? Validate(string token, DateTimeOffset now);
}

public sealed class TokenService(TokenOptions options) : ITokenService
{
    public string Create(TokenUser user, DateTimeOffset now)
    {
        var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var payload = Encode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object?>
        {
            ["sub"] = user.Id,
            ["email"] = user.Email,
            ["role"] = user.Role,
            ["customer_id"] = user.CustomerId,
            ["iss"] = options.Issuer,
            ["aud"] = options.Audience,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.Add(options.Lifetime).ToUnixTimeSeconds()
        }));
        var content = $"{header}.{payload}";
        return $"{content}.{Sign(content)}";
    }

    public ClaimsPrincipal? Validate(string token, DateTimeOffset now)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        var content = $"{parts[0]}.{parts[1]}";
        var expected = Encoding.ASCII.GetBytes(Sign(content));
        var actual = Encoding.ASCII.GetBytes(parts[2]);
        if (expected.Length != actual.Length || !CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            return null;
        }

        try
        {
            using var payload = JsonDocument.Parse(Decode(parts[1]));
            var root = payload.RootElement;
            if (root.GetProperty("iss").GetString() != options.Issuer ||
                root.GetProperty("aud").GetString() != options.Audience ||
                root.GetProperty("exp").GetInt64() <= now.ToUnixTimeSeconds())
            {
                return null;
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, root.GetProperty("sub").GetString()!),
                new(ClaimTypes.Email, root.GetProperty("email").GetString()!),
                new(ClaimTypes.Role, root.GetProperty("role").GetString()!)
            };
            if (root.TryGetProperty("customer_id", out var customerId) && customerId.ValueKind == JsonValueKind.String)
            {
                claims.Add(new Claim("customer_id", customerId.GetString()!));
            }

            return new ClaimsPrincipal(new ClaimsIdentity(claims, KitRentalAuthenticationDefaults.Scheme));
        }
        catch (JsonException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private string Sign(string content)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.Secret));
        return Encode(hmac.ComputeHash(Encoding.ASCII.GetBytes(content)));
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Decode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }
}
