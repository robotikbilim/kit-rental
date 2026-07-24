using KitRental.Web.Mvc.Models;
using KitRental.Web.Mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Web.Mvc.Controllers;

[Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
public sealed class SupplyNeedsController(KitRentalApiClient apiClient) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(
        new SupplyNeedIndexPageViewModel(await apiClient.GetSupplyNeedsAsync(cancellationToken),
            await apiClient.GetStorageLocationsAsync(cancellationToken)));

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
        View("Form", new SupplyNeedFormPageViewModel(new SupplyNeedInputViewModel(),
            await apiClient.GetComponentsAsync(cancellationToken), false));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SupplyNeedInputViewModel model, CancellationToken cancellationToken)
    {
        NormalizeAndValidate(model);
        if (!ModelState.IsValid) return View("Form", await FormPageAsync(model, false, cancellationToken));
        var result = await apiClient.CreateSupplyNeedAsync(model, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "İhtiyaç listesi oluşturulamadı.");
            return View("Form", await FormPageAsync(model, false, cancellationToken));
        }
        TempData["Success"] = "İhtiyaç listesi oluşturuldu.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var list = await apiClient.GetSupplyNeedAsync(id, cancellationToken);
        if (list is null) return NotFound();
        var form = new SupplyNeedInputViewModel { Id = id, Lines = list.Lines.Select(line =>
            new SupplyNeedLineInputViewModel { ComponentId = line.ComponentId, Quantity = line.Quantity }).ToList() };
        return View("Form", await FormPageAsync(form, true, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, SupplyNeedInputViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        NormalizeAndValidate(model);
        if (!ModelState.IsValid) return View("Form", await FormPageAsync(model, true, cancellationToken));
        var result = await apiClient.UpdateSupplyNeedAsync(id, model, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "İhtiyaç listesi güncellenemedi.");
            return View("Form", await FormPageAsync(model, true, cancellationToken));
        }
        TempData["Success"] = "İhtiyaç listesi güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(Guid id, CompleteSupplyNeedViewModel model,
        CancellationToken cancellationToken)
    {
        model.Id = id;
        if (model.Lines.Count == 0 || model.Lines.Any(line => !line.Confirmed))
            ModelState.AddModelError(nameof(model.Lines), "Tüm komponentleri tek tek teyit edin.");
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage).Where(message => message.Length > 0));
            return RedirectToAction(nameof(Index));
        }
        var result = await apiClient.CompleteSupplyNeedAsync(id, model, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Tedarik teyit edildi ve gelen miktarlar stoğa eklendi." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var result = await apiClient.ApproveSupplyNeedRecommendationAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Tavsiye onaylandı ve gerçek ihtiyaç listesi siparişine dönüştü." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshRecommendation(CancellationToken cancellationToken)
    {
        var result = await apiClient.RefreshSupplyNeedRecommendationAsync(cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Tavsiye edilen ihtiyaç listesi güncel stok miktarlarına göre yenilendi." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await apiClient.DeleteSupplyNeedAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "İhtiyaç listesi silindi." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private async Task<SupplyNeedFormPageViewModel> FormPageAsync(SupplyNeedInputViewModel form, bool isEdit,
        CancellationToken cancellationToken) => new(form, await apiClient.GetComponentsAsync(cancellationToken), isEdit);

    private void NormalizeAndValidate(SupplyNeedInputViewModel model)
    {
        model.Lines = model.Lines.Where(line => line.ComponentId != Guid.Empty).ToList();
        if (model.Lines.Count == 0)
            ModelState.AddModelError(nameof(model.Lines), "En az bir komponent ekleyin.");
        if (model.Lines.GroupBy(line => line.ComponentId).Any(group => group.Count() > 1))
            ModelState.AddModelError(nameof(model.Lines), "Aynı komponent listede birden fazla kez kullanılamaz.");
    }
}
