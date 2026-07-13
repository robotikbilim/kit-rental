using KitRental.Web.Mvc.Models;
using KitRental.Web.Mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Web.Mvc.Controllers;

[Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
public sealed class PhysicalKitsController(KitRentalApiClient apiClient) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await apiClient.GetPhysicalKitDashboardAsync(cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var model = await apiClient.GetPhysicalKitAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
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
        var result = await apiClient.CreatePhysicalKitAsync(model, cancellationToken);
        if (!result.IsSuccess || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Fiziksel kit eklenemedi.");
            return View(new CreatePhysicalKitPageViewModel(model, kits));
        }
        TempData["Success"] = $"{result.Data.SerialNumber} seri numaralı kit stoğa eklendi.";
        return RedirectToAction(nameof(Details), new { id = result.Data.Id });
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
}
