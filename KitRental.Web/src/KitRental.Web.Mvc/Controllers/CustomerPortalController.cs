using KitRental.Web.Mvc.Models;
using KitRental.Web.Mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace KitRental.Web.Mvc.Controllers;

[Authorize(Roles = "CustomerAccountManager,CustomerUser")]
public sealed class CustomerPortalController(KitRentalApiClient apiClient) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        return portal is null ? Forbid() : View(portal);
    }

    [HttpGet]
    public async Task<IActionResult> FindKit(string? identifier, CancellationToken cancellationToken)
    {
        var value = identifier?.Trim() ?? string.Empty;
        if (value.Length == 0)
            return View(new PortalKitLookupPageViewModel(string.Empty, false, null));
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        if (portal is null) return Forbid();
        var kit = portal.Kits.FirstOrDefault(item => item.AssignmentStatus == 2 &&
            (string.Equals(item.SerialNumber, value, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(item.QrCode, value, StringComparison.OrdinalIgnoreCase)));
        if (kit is null)
            return View(new PortalKitLookupPageViewModel(value, true,
                "Bu kodla eşleşen, hesabınıza ait aktif bir kiralık kit bulunamadı."));
        return RedirectToAction(nameof(Kit), new { id = kit.ProductUnitId });
    }

    [HttpGet]
    public async Task<IActionResult> Kit(Guid id, CancellationToken cancellationToken)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        if (portal is null) return Forbid();
        var kit = portal.Kits.FirstOrDefault(item => item.ProductUnitId == id && item.AssignmentStatus == 2);
        return kit is null ? NotFound() : View(new PortalKitDetailPageViewModel(kit,
            portal.Faults.Where(fault => fault.ProductUnitId == id).ToArray()));
    }

    [HttpGet]
    public IActionResult Qr(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200) return BadRequest();
        return File(PngByteQRCodeHelper.GetQRCode(value, QRCodeGenerator.ECCLevel.Q, 8), "image/png");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmDelivery(Guid id, CancellationToken cancellationToken)
    {
        var result = await apiClient.ConfirmPortalOrderDeliveryAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Teslimat onaylandı. Kitleriniz artık kullanımınızda görünüyor."
            : result.Error ?? "Teslimat onaylanamadı.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Returns(CancellationToken cancellationToken)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        return portal is null ? Forbid() : View(portal);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> StartReturn(PortalKitReturnSelectionViewModel model, CancellationToken cancellationToken)
    {
        var result = await apiClient.CreatePortalKitReturnAsync(model.AssignmentIds, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "İade süreci başlatıldı. Kargoya verdiğinizde takip bilgilerini girin."
            : result.Error ?? "İade süreci başlatılamadı.";
        return RedirectToAction(nameof(Returns));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkReturnShipped(PortalKitReturnShipmentViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) { TempData["Error"] = "Kargo firması ve takip numarası zorunludur."; return RedirectToAction(nameof(Returns)); }
        var result = await apiClient.ShipPortalKitReturnAsync(model.ReturnId, model.Carrier, model.TrackingNumber, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "İade kargoya verildi olarak işaretlendi." : result.Error ?? "Kargo bilgisi kaydedilemedi.";
        return RedirectToAction(nameof(Returns));
    }

    [HttpGet]
    public async Task<IActionResult> NewRequest(CancellationToken cancellationToken)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        if (portal is null) return Forbid();
        return View(new PortalRentalRequestPageViewModel(new PortalRentalRequestViewModel
        {
            AddressId = portal.Addresses.FirstOrDefault()?.Id ?? Guid.Empty,
            StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
            EndDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(1).AddDays(7))
        }, portal.Addresses, portal.ProductModels));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NewRequest(PortalRentalRequestViewModel model, CancellationToken cancellationToken)
    {
        model.Lines = model.Lines.Where(line => line.ProductModelId != Guid.Empty && line.Quantity > 0).ToList();
        if (model.Lines.Count == 0)
            ModelState.AddModelError(string.Empty, "En az bir eğitim kiti seçmelisiniz.");
        if (model.EndDate <= model.StartDate)
            ModelState.AddModelError(string.Empty, "Bitiş tarihi başlangıç tarihinden sonra olmalıdır.");
        if (ModelState.IsValid)
        {
            var result = await apiClient.CreatePortalRentalRequestAsync(model, cancellationToken);
            if (result.IsSuccess)
            {
                TempData["Success"] = "Kiralama talebiniz yöneticinin onayına gönderildi.";
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError(string.Empty, result.Error ?? "Talep oluşturulamadı.");
        }
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        return portal is null ? Forbid() : View(new PortalRentalRequestPageViewModel(model, portal.Addresses, portal.ProductModels));
    }

    [HttpGet]
    public async Task<IActionResult> NewFault(Guid? assignmentId, CancellationToken cancellationToken)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        if (portal is null) return Forbid();
        var activeKits = portal.Kits.Where(item => item.AssignmentStatus == 2).ToArray();
        return View(new PortalFaultRequestPageViewModel(new PortalFaultRequestViewModel
        {
            AssignmentId = assignmentId.HasValue && activeKits.Any(item => item.AssignmentId == assignmentId)
                ? assignmentId.Value : activeKits.FirstOrDefault()?.AssignmentId ?? Guid.Empty
        }, activeKits));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NewFault(PortalFaultRequestViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await apiClient.CreatePortalFaultAsync(model, cancellationToken);
            if (result.IsSuccess)
            {
                TempData["Success"] = "Arıza kaydınız oluşturuldu. Servis sürecini bu ekrandan takip edebilirsiniz.";
                return RedirectToAction(nameof(Index), new { section = "faults" });
            }
            ModelState.AddModelError(string.Empty, result.Error ?? "Arıza kaydı oluşturulamadı.");
        }
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        return portal is null ? Forbid() : View(new PortalFaultRequestPageViewModel(model,
            portal.Kits.Where(item => item.AssignmentStatus == 2).ToArray()));
    }

    public async Task<IActionResult> Fault(Guid id, CancellationToken cancellationToken)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        var fault = portal?.Faults.SingleOrDefault(item => item.Id == id);
        return fault is null ? NotFound() : View(fault);
    }
}
