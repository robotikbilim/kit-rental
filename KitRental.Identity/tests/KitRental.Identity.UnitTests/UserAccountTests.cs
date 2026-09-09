using KitRental.Identity.Domain;
using KitRental.Security;
using KitRental.SharedKernel;

namespace KitRental.Identity.UnitTests;

public sealed class UserAccountTests
{
    [Fact]
    public void CustomerRoleRequiresCustomerScope()
    {
        var exception = Assert.Throws<DomainException>(() => UserAccount.Create(
            Guid.NewGuid(), "user@example.com", "Müşteri", new PasswordHasher().Hash("Password123!"), UserRole.CustomerUser, null));
        Assert.Equal("user.customer_scope_required", exception.Code);
    }
}
