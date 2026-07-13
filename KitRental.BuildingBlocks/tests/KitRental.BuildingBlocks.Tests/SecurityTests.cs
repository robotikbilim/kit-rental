using System.Security.Claims;
using KitRental.Security;

namespace KitRental.BuildingBlocks.Tests;

public sealed class SecurityTests
{
    [Fact]
    public void PasswordHasher_VerifiesOnlyCorrectPassword()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("StrongPassword123!");
        Assert.True(hasher.Verify("StrongPassword123!", hash));
        Assert.False(hasher.Verify("wrong-password", hash));
    }

    [Fact]
    public void TokenService_RoundTripsIdentityAndCustomerScope()
    {
        var now = new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        var service = new TokenService(new TokenOptions("issuer", "audience", "a-secret-long-enough-for-tests", TimeSpan.FromHours(1)));
        var user = new TokenUser(Guid.NewGuid(), "customer@example.com", "CustomerUser", Guid.NewGuid());
        var principal = service.Validate(service.Create(user, now), now.AddMinutes(1));
        Assert.NotNull(principal);
        Assert.Equal(user.Id.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(user.CustomerId.ToString(), principal.FindFirstValue("customer_id"));
    }
}
