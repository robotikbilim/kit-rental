using Microsoft.AspNetCore.Mvc;

namespace KitRental.Web.Mvc.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index() => User.Identity?.IsAuthenticated != true
        ? RedirectToAction("Login", "Account")
        : User.IsInRole("CustomerAccountManager") || User.IsInRole("CustomerUser")
            ? RedirectToAction("Index", "CustomerPortal")
            : RedirectToAction("Dashboard", "Operations");

    public IActionResult Error() => View();
}
