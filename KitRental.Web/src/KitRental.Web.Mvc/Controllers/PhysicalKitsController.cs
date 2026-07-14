using KitRental.Web.Mvc.Models;
using KitRental.Web.Mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace KitRental.Web.Mvc.Controllers;

[Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
public sealed class PhysicalKitsController(KitRentalApiClient apiClient) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await apiClient.GetPhysicalKitModelSummariesAsync(cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Units(Guid id, string filter = "all", int page = 1,
        CancellationToken cancellationToken = default)
    {
        var model = await apiClient.GetPhysicalKitUnitsAsync(id, filter, page, 20, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var model = await apiClient.GetPhysicalKitAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Lookup(string? identifier, CancellationToken cancellationToken)
    {
        var value = identifier?.Trim() ?? string.Empty;
        if (value.Length == 0)
            return View(new PhysicalKitLookupPageViewModel(string.Empty, false, null, null));
        var result = await apiClient.LookupPhysicalKitAsync(value, cancellationToken);
        return View(new PhysicalKitLookupPageViewModel(value, true, result,
            result is null ? "Bu seri numarası veya QR kodla eşleşen fiziksel kit bulunamadı." : null));
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
        View(new CreatePhysicalKitPageViewModel(new CreatePhysicalKitViewModel(),
            await apiClient.GetProductModelsAsync(cancellationToken)));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePhysicalKitViewModel model, CancellationToken cancellationToken)
    {
        var kits = await apiClient.GetProductModelsAsync(cancellationToken);
        if (!ModelState.IsValid) return View(new CreatePhysicalKitPageViewModel(model, kits));
        var result = await apiClient.CreatePhysicalKitsAsync(model, cancellationToken);
        if (!result.IsSuccess || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Fiziksel kit eklenemedi.");
            return View(new CreatePhysicalKitPageViewModel(model, kits));
        }
        var selectedKit = kits.Single(kit => kit.Id == model.ProductModelId);
        var labels = result.Data.Select(unit => new PhysicalKitLabelViewModel(unit.Id, selectedKit.Name,
            selectedKit.Sku, unit.SerialNumber, unit.QrCode)).ToArray();
        return View("Labels", new PhysicalKitLabelsPageViewModel(DateTimeOffset.Now, labels));
    }

    [HttpGet]
    public IActionResult Qr(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200) return BadRequest();
        var image = PngByteQRCodeHelper.GetQRCode(value, QRCodeGenerator.ECCLevel.Q, 8);
        return File(image, "image/png");
    }

    [HttpGet]
    public async Task<IActionResult> Labels(Guid id, string filter = "all", CancellationToken cancellationToken = default)
    {
        var units = await apiClient.GetPhysicalKitLabelsAsync(id, filter, cancellationToken);
        if (units.Count == 0) return RedirectToAction(nameof(Units), new { id, filter });
        var labels = units.Select(unit => new PhysicalKitLabelViewModel(unit.Id, unit.KitName, unit.KitSku,
            unit.SerialNumber, unit.QrCode)).ToArray();
        var backUrl = Url.Action(nameof(Units), new { id, filter });
        return View(new PhysicalKitLabelsPageViewModel(DateTimeOffset.Now, labels, backUrl));
    }

    [HttpGet]
    public async Task<IActionResult> Rent(Guid id, CancellationToken cancellationToken)
    {
        var detail = await apiClient.GetPhysicalKitAsync(id, cancellationToken);
        if (detail is null) return NotFound();
        if (detail.Kit.Status != 1) return RedirectToAction(nameof(Details), new { id });
        return View(new RentPhysicalKitViewModel { ProductUnitId = id, KitName = detail.Kit.KitName,
            SerialNumber = detail.Kit.SerialNumber, ImageUrl = detail.Kit.ImageUrl,
            StartDate = DateOnly.FromDateTime(DateTime.Today), EndDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(1)) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Rent(RentPhysicalKitViewModel model, CancellationToken cancellationToken)
    {
        if (model.EndDate < model.StartDate) ModelState.AddModelError(nameof(model.EndDate), "Bitiş tarihi başlangıçtan önce olamaz.");
        if (!ModelState.IsValid) return View(model);
        var result = await apiClient.RentPhysicalKitAsync(model, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Kiralama kaydedilemedi.");
            return View(model);
        }
        TempData["Success"] = $"Kiralama kaydedildi. Sipariş: {result.Data!.OrderNumber}";
        return RedirectToAction(nameof(Details), new { id = model.ProductUnitId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PrepareBulkRent(PhysicalKitSelectionViewModel selection,
        CancellationToken cancellationToken)
    {
        selection.ProductUnitIds = selection.ProductUnitIds.Distinct().ToList();
        if (selection.ProductUnitIds.Count == 0)
        {
            TempData["Error"] = "Toplu kiralama için en az bir fiziksel kit seçin.";
            return RedirectToAction(nameof(Units), new { id = selection.ProductModelId, filter = "available" });
        }
        if (selection.ProductUnitIds.Count > 100)
        {
            TempData["Error"] = "Tek işlemde en fazla 100 fiziksel kit kiralanabilir.";
            return RedirectToAction(nameof(Units), new { id = selection.ProductModelId, filter = "available" });
        }

        var details = new List<PhysicalKitDetailViewModel>();
        foreach (var unitId in selection.ProductUnitIds)
        {
            var detail = await apiClient.GetPhysicalKitAsync(unitId, cancellationToken);
            if (detail is null || detail.Kit.ProductModelId != selection.ProductModelId || detail.Kit.Status != 1)
            {
                TempData["Error"] = "Seçilen kitlerden biri artık kiralanabilir değil. Liste yenilendi.";
                return RedirectToAction(nameof(Units), new { id = selection.ProductModelId, filter = "available" });
            }
            details.Add(detail);
        }

        return View("BulkRent", new BulkRentPhysicalKitsViewModel
        {
            ProductModelId = selection.ProductModelId,
            ProductUnitIds = details.Select(item => item.Kit.Id).ToList(),
            SerialNumbers = details.Select(item => item.Kit.SerialNumber).Order().ToList(),
            KitName = details[0].Kit.KitName,
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(1))
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkRent(BulkRentPhysicalKitsViewModel model, CancellationToken cancellationToken)
    {
        model.ProductUnitIds = model.ProductUnitIds.Distinct().ToList();
        if (model.ProductUnitIds.Count == 0)
            ModelState.AddModelError(nameof(model.ProductUnitIds), "En az bir fiziksel kit seçilmelidir.");
        if (model.ProductUnitIds.Count > 100)
            ModelState.AddModelError(nameof(model.ProductUnitIds), "Tek işlemde en fazla 100 fiziksel kit kiralanabilir.");
        if (model.EndDate < model.StartDate)
            ModelState.AddModelError(nameof(model.EndDate), "Bitiş tarihi başlangıçtan önce olamaz.");
        if (!ModelState.IsValid) return View(model);

        var result = await apiClient.BulkRentPhysicalKitsAsync(model, cancellationToken);
        if (!result.IsSuccess || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Toplu kiralama kaydedilemedi.");
            return View(model);
        }

        TempData["Success"] = $"{result.Data.KitCount} kit toplu olarak kiralandı. Sipariş: {result.Data.OrderNumber}";
        return RedirectToAction(nameof(Units), new { id = model.ProductModelId, filter = "available" });
    }
}
