using KitRental.Core.Application.Common;
using KitRental.Security;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace KitRental.Core.Api.Controllers;

public abstract class CoreApiControllerBase : ControllerBase
{
    protected Guid GetRequiredCustomerId() => User.GetCustomerId()
        ?? throw new ForbiddenException("Bu işlem için bir müşteri hesabına bağlı olmalısınız.");

    protected string GetActorDisplayName() => User.Identity?.Name
        ?? User.FindFirstValue(ClaimTypes.Email)
        ?? User.GetRequiredUserId().ToString();

    protected static void EnsureCustomerScope(ClaimsPrincipal user, Guid requestedCustomerId)
    {
        var customerId = user.GetCustomerId();
        if (customerId.HasValue && customerId.Value != requestedCustomerId)
            throw new ForbiddenException("Başka bir müşteri hesabı adına işlem yapılamaz.");
    }

    protected void EnsureCustomerScope(Guid requestedCustomerId) =>
        EnsureCustomerScope(User, requestedCustomerId);
}
