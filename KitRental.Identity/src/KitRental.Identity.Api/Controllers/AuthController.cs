using KitRental.Identity.Api.Contracts.Requests;
using KitRental.Identity.Application;
using KitRental.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace KitRental.Identity.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IdentityService service) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken) =>
        Ok(await service.LoginAsync(new LoginCommand(request.Email, request.Password), cancellationToken));

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me() => Ok(new
    {
        id = User.GetRequiredUserId(),
        email = User.FindFirstValue(ClaimTypes.Email),
        role = User.FindFirstValue(ClaimTypes.Role),
        customerId = User.GetCustomerId()
    });
}
