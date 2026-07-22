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

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReceiveReturn(Guid id, CancellationToken cancellationToken)
    {
        var result = await apiClient.ReceiveKitReturnAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "İade teslim alındı; kitler yeniden kullanılabilir stoka eklendi."
            : result.Error ?? "İade teslim alınamadı.";
        return RedirectToAction(nameof(Dashboard));
    }

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

    [HttpGet]
    public async Task<IActionResult> OrderDetails(Guid id, CancellationToken cancellationToken)
    {
        var model = await apiClient.GetOrderDetailAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> CreateOrder(CancellationToken cancellationToken)
    {
        var customers = (await apiClient.GetCustomersAsync(cancellationToken)).Where(item => item.IsActive).ToArray();
        var model = new AdminOrderInputViewModel
        {
            CustomerId = customers.FirstOrDefault()?.Id ?? Guid.Empty,
            AddressId = customers.FirstOrDefault()?.Addresses.FirstOrDefault()?.Id ?? Guid.Empty,
            StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
            EndDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(1).AddDays(7))
        };
        return View(new AdminOrderPageViewModel(model, customers,
            await apiClient.GetProductModelsAsync(cancellationToken)));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> CreateOrder(AdminOrderInputViewModel model,
        CancellationToken cancellationToken)
    {
        model.Lines = model.Lines.Where(line => line.ProductModelId != Guid.Empty && line.Quantity > 0).ToList();
        if (model.Lines.Count == 0)
            ModelState.AddModelError(string.Empty, "En az bir eğitim kiti seçmelisiniz.");
        if (model.EndDate <= model.StartDate)
            ModelState.AddModelError(string.Empty, "Bitiş tarihi başlangıç tarihinden sonra olmalıdır.");
        if (ModelState.IsValid)
        {
            var result = await apiClient.CreateOrderAsync(model, cancellationToken);
            if (result.IsSuccess)
            {
                TempData["Success"] = "Sipariş oluşturuldu ve onay sırasına alındı.";
                return RedirectToAction(nameof(Orders));
            }
            ModelState.AddModelError(string.Empty, result.Error ?? "Sipariş oluşturulamadı.");
        }
        return View(new AdminOrderPageViewModel(model,
            (await apiClient.GetCustomersAsync(cancellationToken)).Where(item => item.IsActive).ToArray(),
            await apiClient.GetProductModelsAsync(cancellationToken)));
    }

    public async Task<IActionResult> Faults([FromQuery] FaultFilterViewModel filter,
        CancellationToken cancellationToken)
    {
        if (filter.OpenedFrom.HasValue && filter.OpenedTo.HasValue && filter.OpenedFrom > filter.OpenedTo)
        {
            ModelState.AddModelError(nameof(filter.OpenedTo), "Bitiş tarihi başlangıç tarihinden önce olamaz.");
            filter.OpenedTo = null;
        }
        var result = await apiClient.GetFaultsAsync(filter, cancellationToken)
            ?? new FaultPageViewModel(1, filter.PageSize, 0, 1, []);
        return View(new FaultScreenViewModel(result, filter));
    }

    [HttpGet, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> PrepareOrderKits(Guid id, CancellationToken cancellationToken)
    {
        var order = await apiClient.GetOrderDetailAsync(id, cancellationToken);
        if (order is null) return NotFound();
        if (order.Status != 3 || order.Kits.Count > 0)
            return RedirectToAction(nameof(OrderDetails), new { id });
        return View(new PrepareOrderKitsViewModel
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerName = order.CustomerName,
            Lines = order.Lines.Select(line => new PortalRentalLineInputViewModel
            {
                ProductModelId = line.ProductModelId,
                Quantity = line.Quantity
            }).ToList(),
            ProductModels = await apiClient.GetProductModelsAsync(cancellationToken)
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> PrepareOrderKits(PrepareOrderKitsViewModel model,
        CancellationToken cancellationToken)
    {
        model.Lines = model.Lines.Where(line => line.ProductModelId != Guid.Empty && line.Quantity > 0).ToList();
        if (model.Lines.Count == 0)
            ModelState.AddModelError(string.Empty, "En az bir eğitim kiti seçmelisiniz.");
        if (model.Lines.Sum(line => line.Quantity) > 200)
            ModelState.AddModelError(string.Empty, "Tek siparişte en fazla 200 fiziksel kit oluşturulabilir.");
        if (ModelState.IsValid)
        {
            var result = await apiClient.CreateOrderKitsAsync(
                model.OrderId, model.Lines, model.UseAvailableKits, cancellationToken);
            if (result.IsSuccess)
            {
                var data = result.Data!;
                TempData["Success"] = data.ReusedCount > 0
                    ? $"Stoktaki {data.ReusedCount} hazır kit rezerve edildi; eksik {data.CreatedCount} fiziksel kit üretildi."
                    : $"Sipariş kapsamındaki {data.CreatedCount} fiziksel kit oluşturuldu ve rezerve edildi.";
                return RedirectToAction(nameof(OrderDetails), new { id = model.OrderId });
            }
            ModelState.AddModelError(string.Empty, result.Error ?? "Fiziksel kitler oluşturulamadı.");
        }
        var order = await apiClient.GetOrderDetailAsync(model.OrderId, cancellationToken);
        model.OrderNumber = order?.OrderNumber ?? model.OrderNumber;
        model.CustomerName = order?.CustomerName ?? model.CustomerName;
        model.ProductModels = await apiClient.GetProductModelsAsync(cancellationToken);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> UpdateOrderStatus(Guid id, int target, bool returnToDetails,
        CancellationToken cancellationToken)
    {
        if (target is not (3 or 4 or 6 or 7))
            return BadRequest();
        var result = await apiClient.UpdateOrderStatusAsync(id, target, cancellationToken);
        if (result.IsSuccess)
        {
            var statusName = target switch
            {
                3 => "onaylandı",
                4 => "hazırlanıyor",
                6 => "kargoya verildi",
                7 => "teslim edildi",
                _ => "güncellendi"
            };
            TempData["Success"] = $"Sipariş durumu “{statusName}” olarak güncellendi.";
        }
        else
        {
            TempData["Error"] = result.Error;
        }
        return returnToDetails
            ? RedirectToAction(nameof(OrderDetails), new { id })
            : RedirectToAction(nameof(Orders));
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
