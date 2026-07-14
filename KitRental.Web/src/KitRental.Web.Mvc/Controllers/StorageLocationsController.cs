using KitRental.Web.Mvc.Models;
using KitRental.Web.Mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Web.Mvc.Controllers;

[Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
public sealed class StorageLocationsController(KitRentalApiClient apiClient) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await apiClient.GetStorageLocationsAsync(cancellationToken));

    [HttpGet]
    public IActionResult Create() => View("Form", new StorageLocationInputViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StorageLocationInputViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View("Form", model);
        var result = await apiClient.CreateStorageLocationAsync(model, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Raf oluşturulamadı.");
            return View("Form", model);
        }
        TempData["Success"] = "Raf oluşturuldu.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var location = (await apiClient.GetStorageLocationsAsync(cancellationToken))
            .SingleOrDefault(item => item.Id == id);
        return location is null ? NotFound() : View("Form", new StorageLocationInputViewModel
        {
            Id = location.Id,
            Code = location.Code,
            Warehouse = location.Warehouse,
            Aisle = location.Aisle,
            Rack = location.Rack,
            Shelf = location.Shelf
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, StorageLocationInputViewModel model,
        CancellationToken cancellationToken)
    {
        model.Id = id;
        if (!ModelState.IsValid) return View("Form", model);
        var result = await apiClient.UpdateStorageLocationAsync(id, model, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Raf güncellenemedi.");
            return View("Form", model);
        }
        TempData["Success"] = "Raf güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await apiClient.DeleteStorageLocationAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Raf silindi. Bu rafı varsayılan konum olarak kullanan komponentler 'Bilinmiyor' olarak güncellendi."
            : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
