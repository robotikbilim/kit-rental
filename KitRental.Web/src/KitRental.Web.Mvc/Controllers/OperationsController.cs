using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitRental.Web.Mvc.Services;
using KitRental.Web.Mvc.Models;

namespace KitRental.Web.Mvc.Controllers;

[Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff,ServiceTechnician,Auditor")]
public sealed class OperationsController(KitRentalApiClient apiClient) : Controller
{
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken) =>
        View(await apiClient.GetDashboardAsync(cancellationToken));

    public async Task<IActionResult> Inventory([FromQuery] InventoryFilterViewModel filter,
        CancellationToken cancellationToken)
    {
        if (filter.CreatedFrom.HasValue && filter.CreatedTo.HasValue && filter.CreatedFrom > filter.CreatedTo)
        {
            ModelState.AddModelError(nameof(filter.CreatedTo), "Bitiş tarihi başlangıç tarihinden önce olamaz.");
            filter.CreatedTo = null;
        }
        var result = await apiClient.GetInventoryAsync(filter, cancellationToken)
            ?? new InventoryPageViewModel(1, filter.PageSize, 0, 1, []);
        return View(new InventoryScreenViewModel(result, filter,
            await apiClient.GetProductModelsAsync(cancellationToken)));
    }

    public async Task<IActionResult> Orders(CancellationToken cancellationToken) =>
        View(await apiClient.GetOrdersAsync(cancellationToken));

    public async Task<IActionResult> Faults(CancellationToken cancellationToken) =>
        View(await apiClient.GetFaultsAsync(cancellationToken));

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> ApproveOrder(Guid id, CancellationToken cancellationToken)
    {
        var result = await apiClient.ApproveOrderAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Kiralama talebi onaylandı ve hazırlık sırasına alındı." : result.Error;
        return RedirectToAction(nameof(Orders));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SystemAdmin,OperationsManager,ServiceTechnician")]
    public async Task<IActionResult> UpdateFault(Guid id, int status, string note, CancellationToken cancellationToken)
    {
        var result = await apiClient.ChangeFaultStatusAsync(id, status, note, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Arıza süreci güncellendi; müşteri portalına yansıtıldı." : result.Error;
        return RedirectToAction(nameof(Faults));
    }
}
