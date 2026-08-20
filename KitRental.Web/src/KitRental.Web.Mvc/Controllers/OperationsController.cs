using KitRental.Web.Mvc.Models;
using KitRental.Web.Mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Web.Mvc.Controllers;

[Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff,ServiceTechnician,Auditor")]
public sealed class OperationsController(KitRentalApiClient apiClient) : Controller
{
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken) =>
        View(await apiClient.GetDashboardAsync(cancellationToken));

    [HttpGet, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> EmailHistory(CancellationToken cancellationToken) =>
        View(await apiClient.GetEmailDeliveriesAsync(cancellationToken));

    [HttpGet, Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> Audit([FromQuery] AuditFilterViewModel filter,
        CancellationToken cancellationToken)
    {
        if (filter.OccurredFrom.HasValue && filter.OccurredTo.HasValue &&
            filter.OccurredFrom > filter.OccurredTo)
        {
            ModelState.AddModelError(nameof(filter.OccurredTo), "Bitiş tarihi başlangıç tarihinden önce olamaz.");
            filter.OccurredTo = null;
        }
        var users = await apiClient.GetUsersAsync(cancellationToken);
        var result = await apiClient.GetAuditAsync(filter, cancellationToken)
            ?? new AuditPageApiResponse(1, filter.PageSize, 0, 1, []);
        var userMap = users.ToDictionary(item => item.Id);
        var items = result.Items.Select(item =>
        {
            userMap.TryGetValue(item.ActorId, out var actor);
            return new AuditListItemViewModel(item.Id, actor?.DisplayName ?? "Bilinmeyen kullanıcı",
                actor?.Email ?? item.ActorId.ToString(), actor?.Role is 5 or 6,
                item.EntityType, item.EntityId, item.Action, item.PreviousValue, item.NewValue, item.OccurredAt);
        }).ToArray();
        return View(new AuditScreenViewModel(result.Page, result.PageSize, result.TotalCount,
            result.TotalPages, items, filter, users.OrderBy(item => item.DisplayName).ToArray()));
    }

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

    public async Task<IActionResult> Orders(int? type, CancellationToken cancellationToken)
    {
        var orders = await apiClient.GetOrdersAsync(cancellationToken);
        ViewBag.OrderType = type;
        return View(type is 1 or 2 ? orders.Where(item => item.Type == type).ToArray() : orders);
    }

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

    [HttpGet, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> CreatePurchaseOrder(CancellationToken cancellationToken)
    {
        var customers = (await apiClient.GetCustomersAsync(cancellationToken)).Where(item => item.IsActive).ToArray();
        var model = new PurchaseOrderInputViewModel
        {
            CustomerId = customers.FirstOrDefault()?.Id ?? Guid.Empty,
            AddressId = customers.FirstOrDefault()?.Addresses.FirstOrDefault()?.Id ?? Guid.Empty
        };
        return View(new PurchaseOrderPageViewModel(model, customers,
            await apiClient.GetProductModelsAsync(cancellationToken)));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> CreatePurchaseOrder(PurchaseOrderInputViewModel model,
        CancellationToken cancellationToken)
    {
        model.Lines = model.Lines.Where(line => line.ProductModelId != Guid.Empty && line.Quantity > 0).ToList();
        if (model.Lines.Count == 0)
            ModelState.AddModelError(string.Empty, "En az bir eğitim kiti seçmelisiniz.");
        if (ModelState.IsValid)
        {
            var result = await apiClient.CreatePurchaseOrderAsync(model, cancellationToken);
            if (result.IsSuccess)
            {
                TempData["Success"] = "Satın alma siparişi onaylanmış olarak oluşturuldu.";
                return RedirectToAction(nameof(OrderDetails), new { id = result.Data!.Id });
            }
            ModelState.AddModelError(string.Empty, result.Error ?? "Satın alma siparişi oluşturulamadı.");
        }
        return View(new PurchaseOrderPageViewModel(model,
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
    public async Task<IActionResult> FaultGuide(Guid? editId, CancellationToken cancellationToken)
    {
        var entries = await apiClient.GetFaultGuideEntriesAsync(cancellationToken);
        var edit = editId.HasValue ? entries.SingleOrDefault(item => item.Id == editId.Value) : null;
        var form = edit is null
            ? new FaultGuideEntryInputViewModel
            {
                DisplayOrder = entries.Count == 0 ? 10 : entries.Max(item => item.DisplayOrder) + 10
            }
            : new FaultGuideEntryInputViewModel
            {
                Id = edit.Id,
                Title = edit.Title,
                Problem = edit.Problem,
                Solution = edit.Solution,
                DisplayOrder = edit.DisplayOrder,
                IsActive = edit.IsActive
            };
        return View(new FaultGuidePageViewModel(entries, form));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> SaveFaultGuide(FaultGuideEntryInputViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View("FaultGuide", new FaultGuidePageViewModel(
                await apiClient.GetFaultGuideEntriesAsync(cancellationToken), model));
        var result = model.Id.HasValue
            ? await apiClient.UpdateFaultGuideEntryAsync(model, cancellationToken)
            : await apiClient.CreateFaultGuideEntryAsync(model, cancellationToken);
        if (result.IsSuccess)
        {
            TempData["Success"] = model.Id.HasValue
                ? "Problem rehberi guncellendi."
                : "Problem rehberi eklendi.";
            return RedirectToAction(nameof(FaultGuide));
        }
        ModelState.AddModelError(string.Empty, result.Error ?? "Problem rehberi kaydedilemedi.");
        return View("FaultGuide", new FaultGuidePageViewModel(
            await apiClient.GetFaultGuideEntriesAsync(cancellationToken), model));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> DeleteFaultGuide(Guid id, CancellationToken cancellationToken)
    {
        var result = await apiClient.DeleteFaultGuideEntryAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Problem rehberi silindi."
            : result.Error ?? "Problem rehberi silinemedi.";
        return RedirectToAction(nameof(FaultGuide));
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
            ProductModels = await apiClient.GetProductModelsAsync(cancellationToken),
            RentalCohortId = order.RentalCohortId,
            RentalCohorts = order.Type == 1
                ? await apiClient.GetCustomerRentalCohortsAsync(order.CustomerId, cancellationToken)
                : []
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> PrepareOrderKits(PrepareOrderKitsViewModel model,
        CancellationToken cancellationToken)
    {
        model.Lines = model.Lines.Where(line => line.ProductModelId != Guid.Empty && line.Quantity > 0).ToList();
        if (model.Lines.Count == 0)
            ModelState.AddModelError(string.Empty, "En az bir eğitim kiti seçmelisiniz.");
        if (ModelState.IsValid)
        {
            var result = await apiClient.CreateOrderKitsAsync(
                model.OrderId, model.Lines, model.UseAvailableKits, model.RentalCohortId, cancellationToken);
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
        model.RentalCohorts = order is not null && order.Type == 1
            ? await apiClient.GetCustomerRentalCohortsAsync(order.CustomerId, cancellationToken)
            : [];
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
