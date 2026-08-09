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
    public async Task<IActionResult> Index(string qrCode, CancellationToken cancellationToken)
    {
        if (RedirectAuthenticated(qrCode) is { } redirect) return redirect;

        var kit = await apiClient.GetPublicFaultKitAsync(qrCode, cancellationToken);
        return kit is null ? NotFound() : View(new PublicKitActionViewModel(kit.QrCode, kit.KitName, kit.SerialNumber));
    }

    [HttpGet("{qrCode}/ariza")]
    public async Task<IActionResult> Troubleshooting(string qrCode, CancellationToken cancellationToken)
    {
        if (RedirectAuthenticated(qrCode) is { } redirect) return redirect;
        var kit = await apiClient.GetPublicFaultKitAsync(qrCode, cancellationToken);
        if (kit is null) return NotFound();
        return View(new PublicFaultTroubleshootingViewModel(kit.QrCode, kit.KitName, kit.SerialNumber,
            await apiClient.GetPublicFaultGuideEntriesAsync(cancellationToken)));
    }

    [HttpGet("{qrCode}/ariza/olustur")]
    public async Task<IActionResult> Report(string qrCode, CancellationToken cancellationToken)
    {
        if (RedirectAuthenticated(qrCode) is { } redirect) return redirect;
        var kit = await apiClient.GetPublicFaultKitAsync(qrCode, cancellationToken);
        return kit is null ? NotFound() : View(new PublicFaultFormViewModel
        {
            QrCode = kit.QrCode, KitName = kit.KitName, SerialNumber = kit.SerialNumber
        });
    }

    [HttpPost("{qrCode}/ariza/olustur"), ValidateAntiForgeryToken]
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
            ModelState.AddModelError(string.Empty, result.Error ?? "Ariza kaydi olusturulamadi.");
            return View(model);
        }
        ViewData["SuccessTitle"] = "Ariza kaydi olusturuldu";
        ViewData["SuccessMessage"] = "Ariza kaydiniz teknik ekibin ekranina acik kayit olarak dustu.";
        return View("Success", new PublicKitActionViewModel(model.QrCode, model.KitName, model.SerialNumber));
    }

    [HttpGet("{qrCode}/iade")]
    public async Task<IActionResult> Return(string qrCode, CancellationToken cancellationToken)
    {
        if (RedirectAuthenticated(qrCode) is { } redirect) return redirect;
        var kit = await apiClient.GetPublicFaultKitAsync(qrCode, cancellationToken);
        return kit is null ? NotFound() : View(new PublicReturnFormViewModel
        {
            QrCode = kit.QrCode, KitName = kit.KitName, SerialNumber = kit.SerialNumber
        });
    }

    [HttpPost("{qrCode}/iade"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Return(string qrCode, PublicReturnFormViewModel model,
        CancellationToken cancellationToken)
    {
        model.QrCode = qrCode;
        var kit = await apiClient.GetPublicFaultKitAsync(qrCode, cancellationToken);
        if (kit is null) return NotFound();
        model.KitName = kit.KitName;
        model.SerialNumber = kit.SerialNumber;
        if (!ModelState.IsValid) return View(model);
        var result = await apiClient.CreatePublicReturnAsync(model, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Iade talebi olusturulamadi.");
            return View(model);
        }
        ViewData["SuccessTitle"] = "Iade talebi olusturuldu";
        ViewData["SuccessMessage"] = "Iade talebiniz operasyon ekibinin ekranina dustu.";
        return View("Success", new PublicKitActionViewModel(model.QrCode, model.KitName, model.SerialNumber));
    }

    private IActionResult? RedirectAuthenticated(string qrCode)
    {
        if (User.Identity?.IsAuthenticated != true) return null;
        return User.IsInRole("CustomerAccountManager") || User.IsInRole("CustomerUser")
            ? RedirectToAction("FindKit", "CustomerPortal", new { identifier = qrCode })
            : RedirectToAction("Lookup", "PhysicalKits", new { identifier = qrCode });
    }
}
