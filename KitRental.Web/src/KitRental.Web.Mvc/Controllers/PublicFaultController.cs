using KitRental.Web.Mvc.Models;
using KitRental.Web.Mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Web.Mvc.Controllers;

[AllowAnonymous]
[Route("ariza")]
public sealed class PublicFaultController(KitRentalApiClient apiClient) : Controller
{
    [HttpGet("{qrCode}")]
    public async Task<IActionResult> Report(string qrCode, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("CustomerAccountManager") || User.IsInRole("CustomerUser"))
                return RedirectToAction("FindKit", "CustomerPortal", new { identifier = qrCode });

            return RedirectToAction("Lookup", "PhysicalKits", new { identifier = qrCode });
        }

        var kit = await apiClient.GetPublicFaultKitAsync(qrCode, cancellationToken);
        return kit is null ? NotFound() : View(new PublicFaultFormViewModel
        {
            QrCode = kit.QrCode, KitName = kit.KitName, SerialNumber = kit.SerialNumber
        });
    }

    [HttpPost("{qrCode}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(string qrCode, PublicFaultFormViewModel model,
        CancellationToken cancellationToken)
    {
        model.QrCode = qrCode;
        var kit = await apiClient.GetPublicFaultKitAsync(qrCode, cancellationToken);
        if (kit is null) return NotFound();
        model.KitName = kit.KitName;
        model.SerialNumber = kit.SerialNumber;
        if (!ModelState.IsValid) return View(model);
        var result = await apiClient.CreatePublicFaultAsync(model, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Arıza bildirimi gönderilemedi.");
            return View(model);
        }
        return View("Success", model);
    }
}
