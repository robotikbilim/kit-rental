using KitRental.Web.Mvc.Models;
using KitRental.Web.Mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Web.Mvc.Controllers;

[Authorize(Roles = "SystemAdmin")]
public sealed class AdminUsersController(KitRentalApiClient apiClient) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var users = (await apiClient.GetUsersAsync(cancellationToken))
            .Where(user => user.Role == 1)
            .OrderBy(user => user.DisplayName)
            .ToArray();
        return View(new AdminUsersPageViewModel(users));
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateAdminUserViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAdminUserViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await apiClient.CreateAdminUserAsync(model, cancellationToken);
            if (result.IsSuccess)
            {
                TempData["Success"] = $"{model.DisplayName} için sistem yöneticisi hesabı oluşturuldu.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Error ?? "Yönetici hesabı oluşturulamadı.");
        }

        return View(model);
    }
}
