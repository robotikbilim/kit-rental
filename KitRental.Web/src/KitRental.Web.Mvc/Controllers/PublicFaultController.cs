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

        var access = await apiClient.CreatePublicFormAccessTokenAsync(qrCode, cancellationToken);
        return access is null
            ? NotFound()
            : RedirectToAction(nameof(Form), new { token = access.Token });
    }

    [HttpGet("form/{token}")]
    public async Task<IActionResult> Form(string token, CancellationToken cancellationToken)
    {
        var kit = await apiClient.GetPublicFaultKitAsync(token, cancellationToken);
        return kit is null
            ? View("LinkExpired")
            : View("Index", new PublicKitActionViewModel(kit.QrCode, kit.KitName, kit.SerialNumber, token));
    }

    [HttpGet("form/{token}/ariza")]
    public async Task<IActionResult> Troubleshooting(string token, CancellationToken cancellationToken)
    {
        var kit = await apiClient.GetPublicFaultKitAsync(token, cancellationToken);
        if (kit is null) return View("LinkExpired");
        return View(new PublicFaultTroubleshootingViewModel(kit.QrCode, kit.KitName, kit.SerialNumber, token,
            await apiClient.GetPublicFaultGuideEntriesAsync(token, cancellationToken)));
    }

    [HttpGet("form/{token}/ariza/olustur")]
    public async Task<IActionResult> Report(string token, CancellationToken cancellationToken)
    {
        var kit = await apiClient.GetPublicFaultKitAsync(token, cancellationToken);
        if (kit is null) return View("LinkExpired");
        var faultContext = await apiClient.GetPublicFaultContextAsync(token, cancellationToken);
        var deliveryContext = await apiClient.GetPublicKitDeliveryContextAsync(token, cancellationToken);
        return View(new PublicFaultFormViewModel
        {
            FaultId = faultContext?.FaultId,
            QrCode = kit.QrCode,
            AccessToken = token,
            KitName = kit.KitName,
            SerialNumber = kit.SerialNumber,
            ReporterName = faultContext?.ReporterName
                ?? deliveryContext?.RecipientName
                ?? string.Empty,
            ReporterPhone = faultContext?.ReporterPhone
                ?? deliveryContext?.RecipientPhone
                ?? string.Empty,
            City = faultContext?.City
                ?? deliveryContext?.City
                ?? string.Empty,
            District = faultContext?.District
                ?? deliveryContext?.District
                ?? string.Empty,
            ReporterAddress = faultContext?.ReporterAddress
                ?? deliveryContext?.AddressLine
                ?? string.Empty,
            Description = faultContext?.Description ?? string.Empty,
            Latitude = faultContext?.Latitude ?? deliveryContext?.Latitude,
            Longitude = faultContext?.Longitude ?? deliveryContext?.Longitude
        });
    }

    [HttpPost("form/{token}/ariza/olustur"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(string token, PublicFaultFormViewModel model,
        CancellationToken cancellationToken)
    {
        model.AccessToken = token;
        var kit = await apiClient.GetPublicFaultKitAsync(token, cancellationToken);
        if (kit is null) return View("LinkExpired");
        var faultContext = await apiClient.GetPublicFaultContextAsync(token, cancellationToken);
        model.QrCode = kit.QrCode;
        model.KitName = kit.KitName;
        model.SerialNumber = kit.SerialNumber;
        model.FaultId = faultContext?.FaultId;
        if (!ModelState.IsValid) return View(model);
        var result = await apiClient.CreatePublicFaultAsync(model, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Ariza kaydi olusturulamadi.");
            return View(model);
        }
        ViewData["SuccessTitle"] = "Ariza kaydi olusturuldu";
        ViewData["SuccessMessage"] = "Ariza kaydiniz teknik ekibin ekranina acik kayit olarak dustu.";
        return View("Success", new PublicKitActionViewModel(model.QrCode, model.KitName, model.SerialNumber, token));
    }

    [HttpGet("form/{token}/iade")]
    public async Task<IActionResult> Return(string token, CancellationToken cancellationToken)
    {
        var kit = await apiClient.GetPublicFaultKitAsync(token, cancellationToken);
        if (kit is null) return View("LinkExpired");
        var returnContext = await apiClient.GetPublicKitReturnContextAsync(token, cancellationToken);
        var deliveryContext = await apiClient.GetPublicKitDeliveryContextAsync(token, cancellationToken);
        return View(new PublicReturnFormViewModel
        {
            QrCode = kit.QrCode,
            AccessToken = token,
            KitName = kit.KitName,
            SerialNumber = kit.SerialNumber,
            ReturnReason = returnContext?.ReturnReason,
            DeliveryMethod = returnContext?.DeliveryMethod ?? 1,
            RequesterName = returnContext?.RequesterName ?? deliveryContext?.RecipientName ?? string.Empty,
            RequesterPhone = returnContext?.RequesterPhone ?? deliveryContext?.RecipientPhone ?? string.Empty,
            City = returnContext?.City ?? deliveryContext?.City ?? string.Empty,
            District = returnContext?.District ?? deliveryContext?.District ?? string.Empty,
            ReturnAddress = returnContext?.ReturnAddress ?? deliveryContext?.AddressLine ?? string.Empty,
            Latitude = returnContext?.Latitude ?? deliveryContext?.Latitude,
            Longitude = returnContext?.Longitude ?? deliveryContext?.Longitude
        });
    }

    [HttpPost("form/{token}/iade"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Return(string token, PublicReturnFormViewModel model,
        CancellationToken cancellationToken)
    {
        model.AccessToken = token;
        var kit = await apiClient.GetPublicFaultKitAsync(token, cancellationToken);
        if (kit is null) return View("LinkExpired");
        model.QrCode = kit.QrCode;
        model.KitName = kit.KitName;
        model.SerialNumber = kit.SerialNumber;
        if (!ModelState.IsValid) return View(model);
        var result = await apiClient.CreatePublicReturnAsync(model, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Iade talebi olusturulamadi.");
            return View(model);
        }
        ViewData["SuccessTitle"] = "Iade talebi kaydedildi";
        ViewData["SuccessMessage"] = "Iade talebiniz operasyon ekibinin ekranina dustu.";
        if (model.DeliveryMethod == 2)
        {
            ViewData["PopupTitle"] = "İade Kodu";
            ViewData["PopupMessage"] = "\"1234567890\" İade Kodu ile herhangi bir Aras Kargo şubesine bırakabilirsiniz.";
        }
        return View("Success", new PublicKitActionViewModel(model.QrCode, model.KitName, model.SerialNumber, token));
    }

    [HttpGet("form/{token}/teslim-al")]
    public async Task<IActionResult> Delivery(string token, CancellationToken cancellationToken)
    {
        var kit = await apiClient.GetPublicFaultKitAsync(token, cancellationToken);
        if (kit is null) return View("LinkExpired");
        var deliveryContext = await apiClient.GetPublicKitDeliveryContextAsync(token, cancellationToken);
        return View(new PublicDeliveryFormViewModel
        {
            QrCode = kit.QrCode,
            AccessToken = token,
            KitName = kit.KitName,
            SerialNumber = kit.SerialNumber,
            RecipientName = deliveryContext?.RecipientName ?? string.Empty,
            RecipientPhone = deliveryContext?.RecipientPhone ?? string.Empty,
            City = deliveryContext?.City ?? string.Empty,
            District = deliveryContext?.District ?? string.Empty,
            AddressLine = deliveryContext?.AddressLine ?? string.Empty,
            Latitude = deliveryContext?.Latitude,
            Longitude = deliveryContext?.Longitude
        });
    }

    [HttpPost("form/{token}/teslim-al"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delivery(string token, PublicDeliveryFormViewModel model,
        CancellationToken cancellationToken)
    {
        model.AccessToken = token;
        var kit = await apiClient.GetPublicFaultKitAsync(token, cancellationToken);
        if (kit is null) return View("LinkExpired");
        model.QrCode = kit.QrCode;
        model.KitName = kit.KitName;
        model.SerialNumber = kit.SerialNumber;
        if (!ModelState.IsValid) return View(model);
        var result = await apiClient.CreatePublicDeliveryAsync(model, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Kit teslim kaydi olusturulamadi.");
            return View(model);
        }
        ViewData["SuccessTitle"] = "Kit teslim alindi";
        ViewData["SuccessMessage"] = "Teslim bilginiz kit gecmisine islendi.";
        return View("Success", new PublicKitActionViewModel(model.QrCode, model.KitName, model.SerialNumber, token));
    }

    private IActionResult? RedirectAuthenticated(string qrCode)
    {
        if (User.Identity?.IsAuthenticated != true) return null;
        return User.IsInRole("CustomerAccountManager") || User.IsInRole("CustomerUser")
            ? RedirectToAction("FindKit", "CustomerPortal", new { identifier = qrCode })
            : RedirectToAction("Lookup", "PhysicalKits", new { identifier = qrCode });
    }
}
