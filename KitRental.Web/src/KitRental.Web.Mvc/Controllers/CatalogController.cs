using KitRental.Web.Mvc.Models;
using KitRental.Web.Mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Web.Mvc.Controllers;

[Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
public sealed class CatalogController(KitRentalApiClient apiClient) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Kits(CancellationToken cancellationToken) =>
        View(await apiClient.GetProductModelsAsync(cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Kit(Guid id, CancellationToken cancellationToken)
    {
        var kit = await apiClient.GetProductModelAsync(id, cancellationToken);
        if (kit is null) return NotFound();
        return View(new KitDetailPageViewModel(kit, await apiClient.GetBomAsync(id, cancellationToken)));
    }

    [HttpGet]
    public async Task<IActionResult> CreateKit(CancellationToken cancellationToken) =>
        View(await KitPageAsync(new CreateKitViewModel(), cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateKit(CreateKitViewModel model, CancellationToken cancellationToken)
    {
        model.Lines = model.Lines.Where(line => line.ComponentId != Guid.Empty).ToList();
        if (model.Lines.Count == 0)
            ModelState.AddModelError(nameof(model.Lines), "Reçeteye en az bir komponent ekleyin.");
        if (model.Lines.GroupBy(line => line.ComponentId).Any(group => group.Count() > 1))
            ModelState.AddModelError(nameof(model.Lines), "Aynı komponent reçetede birden fazla kez kullanılamaz.");
        if (!ModelState.IsValid)
            return View(await KitPageAsync(model, cancellationToken));
        var result = await apiClient.CreateKitAsync(model, cancellationToken);
        if (!result.IsSuccess || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Eğitim kiti oluşturulamadı.");
            return View(await KitPageAsync(model, cancellationToken));
        }
        TempData["Success"] = "Eğitim kiti ve reçetesi oluşturuldu.";
        return RedirectToAction(nameof(Kit), new { id = result.Data.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Components(CancellationToken cancellationToken) =>
        View(await apiClient.GetComponentsAsync(cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Component(Guid id, CancellationToken cancellationToken)
    {
        var component = await apiClient.GetComponentLocatorAsync(id, cancellationToken);
        return component is null ? NotFound() : View(component);
    }

    [HttpGet]
    public IActionResult CreateComponent() => View(new CreateComponentViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateComponent(CreateComponentViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await apiClient.CreateComponentAsync(model, cancellationToken);
        if (!result.IsSuccess || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Komponent oluşturulamadı.");
            return View(model);
        }
        TempData["Success"] = "Komponent oluşturuldu.";
        return RedirectToAction(nameof(Component), new { id = result.Data.Id });
    }

    private async Task<CreateKitPageViewModel> KitPageAsync(CreateKitViewModel form, CancellationToken cancellationToken) =>
        new(form, await apiClient.GetComponentsAsync(cancellationToken));
}
