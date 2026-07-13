using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using KitRental.Web.Mvc.Models;
using KitRental.Web.Mvc.Services;

namespace KitRental.Web.Mvc.Controllers;

public sealed class AccountController(KitRentalApiClient apiClient) : Controller
{
    [HttpGet]
    public IActionResult Login() => View(new LoginViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);
        var result = await apiClient.LoginAsync(model.Email, model.Password, cancellationToken);
        if (result is null)
        {
            model.Error = "E-posta veya parola hatalı.";
            return View(model);
        }

        var roleName = result.User.Role switch
        {
            1 => "SystemAdmin", 2 => "OperationsManager", 3 => "WarehouseStaff",
            4 => "ServiceTechnician", 5 => "CustomerAccountManager", 6 => "CustomerUser", 7 => "Auditor",
            _ => result.User.Role.ToString()
        };
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.User.Id.ToString()),
            new(ClaimTypes.Name, result.User.DisplayName),
            new(ClaimTypes.Email, result.User.Email),
            new(ClaimTypes.Role, roleName)
        };
        if (result.User.CustomerId.HasValue)
            claims.Add(new Claim("customer_id", result.User.CustomerId.Value.ToString()));
        var properties = new AuthenticationProperties { ExpiresUtc = result.ExpiresAt, IsPersistent = true };
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = result.AccessToken }]);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            properties);
        return result.User.CustomerId.HasValue
            ? RedirectToAction("Index", "CustomerPortal")
            : RedirectToAction("Dashboard", "Operations");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    public IActionResult AccessDenied() => View();
}
