using KitRental.Identity.Api.Contracts.Requests;
using KitRental.Identity.Application;
using KitRental.Identity.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Identity.Api.Controllers;

[ApiController]
[Route("api/internal/notification-recipients")]
public sealed class InternalNotificationsController(IdentityService service, IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("admins")]
    public async Task<IActionResult> GetAdmins(CancellationToken cancellationToken)
    {
        var expectedKey = configuration["InternalApiKey"]
            ?? "development-only-internal-key-change-before-production";
        if (string.IsNullOrWhiteSpace(expectedKey) ||
            !Request.Headers.TryGetValue("X-Internal-Api-Key", out var suppliedKey) ||
            !string.Equals(suppliedKey.ToString(), expectedKey, StringComparison.Ordinal))
            return Unauthorized();

        var recipients = (await service.GetUsersAsync(cancellationToken))
            .Where(user => user.IsActive && user.Role is not (UserRole.CustomerAccountManager or UserRole.CustomerUser))
            .Select(user => new NotificationRecipientResponse(user.Email, user.DisplayName))
            .DistinctBy(user => user.Email, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Ok(recipients);
    }
}
