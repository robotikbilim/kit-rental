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
        TempData["Success"] = model.Lines.Count == 0
            ? "Eğitim kiti oluşturuldu. Reçeteyi daha sonra ekleyebilirsiniz."
            : "Eğitim kiti ve reçetesi oluşturuldu.";
        return RedirectToAction(nameof(Kit), new { id = result.Data.Id });
    }

    [HttpGet]
    public async Task<IActionResult> EditRecipe(Guid id, CancellationToken cancellationToken)
    {
        var kit = await apiClient.GetProductModelAsync(id, cancellationToken);
        if (kit is null) return NotFound();
        var bom = await apiClient.GetBomAsync(id, cancellationToken);
        var form = new EditRecipeViewModel
        {
            ProductModelId = id,
            ProductName = kit.Name,
            Version = (bom?.Version ?? 0) + 1,
            Lines = bom?.Lines.Select(line => new CreateKitBomLineViewModel
            {
                ComponentId = line.ComponentId,
                Quantity = line.Quantity
            }).ToList() ?? []
        };
        return View(await RecipePageAsync(form, bom is not null, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRecipe(Guid id, EditRecipeViewModel model, CancellationToken cancellationToken)
    {
        model.ProductModelId = id;
        model.Lines = model.Lines.Where(line => line.ComponentId != Guid.Empty).ToList();
        if (model.Lines.Count == 0)
            ModelState.AddModelError(nameof(model.Lines), "Reçeteye en az bir komponent ekleyin.");
        if (model.Lines.GroupBy(line => line.ComponentId).Any(group => group.Count() > 1))
            ModelState.AddModelError(nameof(model.Lines), "Aynı komponent reçetede birden fazla kez kullanılamaz.");
        var existing = await apiClient.GetBomAsync(id, cancellationToken);
        if (!ModelState.IsValid)
            return View(await RecipePageAsync(model, existing is not null, cancellationToken));
        var result = await apiClient.SaveBomAsync(model, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Reçete kaydedilemedi.");
            return View(await RecipePageAsync(model, existing is not null, cancellationToken));
        }
        TempData["Success"] = existing is null ? "Reçete oluşturuldu." : $"Reçete sürüm {model.Version} olarak güncellendi.";
        return RedirectToAction(nameof(Kit), new { id });
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
    public async Task<IActionResult> CreateComponent(CancellationToken cancellationToken) =>
        View(await ComponentFormPageAsync(new CreateComponentViewModel(), false, cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateComponent(CreateComponentViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(await ComponentFormPageAsync(model, false, cancellationToken));
        var result = await apiClient.CreateComponentAsync(model, cancellationToken);
        if (!result.IsSuccess || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Komponent oluşturulamadı.");
            return View(await ComponentFormPageAsync(model, false, cancellationToken));
        }
        TempData["Success"] = "Komponent oluşturuldu.";
        return RedirectToAction(nameof(Component), new { id = result.Data.Id });
    }

    [HttpGet]
    public async Task<IActionResult> EditKit(Guid id, CancellationToken cancellationToken)
    {
        var kit = await apiClient.GetProductModelAsync(id, cancellationToken);
        return kit is null ? NotFound() : View(new EditKitViewModel { Id = kit.Id, Name = kit.Name, Sku = kit.Sku,
            Description = kit.Description, ImageUrl = kit.ImageUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditKit(Guid id, EditKitViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        if (!ModelState.IsValid) return View(model);
        var result = await apiClient.UpdateKitAsync(id, model, cancellationToken);
        if (!result.IsSuccess) { ModelState.AddModelError(string.Empty, result.Error ?? "Eğitim seti güncellenemedi."); return View(model); }
        TempData["Success"] = "Eğitim seti güncellendi.";
        return RedirectToAction(nameof(Kits));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteKit(Guid id, CancellationToken cancellationToken)
    {
        var result = await apiClient.DeleteKitAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Eğitim seti silindi." : result.Error;
        return RedirectToAction(nameof(Kits));
    }

    [HttpGet]
    public async Task<IActionResult> EditComponent(Guid id, CancellationToken cancellationToken)
    {
        var item = (await apiClient.GetComponentsAsync(cancellationToken))
            .SingleOrDefault(component => component.Id == id);
        if (item is null) return NotFound();
        var form = new EditComponentViewModel { Id = item.Id, Name = item.Name, Sku = item.Sku,
            UnitOfMeasure = item.UnitOfMeasure, MinimumStock = item.MinimumStock, ImageUrl = item.ImageUrl,
            DefaultStorageLocationId = item.DefaultStorageLocationId };
        return View(await ComponentFormPageAsync(form, true, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditComponent(Guid id, EditComponentViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        if (!ModelState.IsValid) return View(await ComponentFormPageAsync(model, true, cancellationToken));
        var result = await apiClient.UpdateComponentAsync(id, model, cancellationToken);
        if (!result.IsSuccess) { ModelState.AddModelError(string.Empty, result.Error ?? "Komponent güncellenemedi."); return View(await ComponentFormPageAsync(model, true, cancellationToken)); }
        TempData["Success"] = "Komponent güncellendi.";
        return RedirectToAction(nameof(Components));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComponent(Guid id, CancellationToken cancellationToken)
    {
        var result = await apiClient.DeleteComponentAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Komponent silindi." : result.Error;
        return RedirectToAction(nameof(Components));
    }

    private async Task<CreateKitPageViewModel> KitPageAsync(CreateKitViewModel form, CancellationToken cancellationToken) =>
        new(form, await apiClient.GetComponentsAsync(cancellationToken));

    private async Task<ComponentFormPageViewModel> ComponentFormPageAsync(CreateComponentViewModel form, bool isEdit,
        CancellationToken cancellationToken) =>
        new(form, await apiClient.GetStorageLocationsAsync(cancellationToken), isEdit);

    private async Task<EditRecipePageViewModel> RecipePageAsync(EditRecipeViewModel form, bool hasExistingRecipe,
        CancellationToken cancellationToken)
    {
        var kit = await apiClient.GetProductModelAsync(form.ProductModelId, cancellationToken);
        if (kit is not null) form.ProductName = kit.Name;
        return new EditRecipePageViewModel(form, await apiClient.GetComponentsAsync(cancellationToken), hasExistingRecipe);
    }
}
