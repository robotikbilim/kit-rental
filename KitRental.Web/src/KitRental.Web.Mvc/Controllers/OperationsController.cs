using ClosedXML.Excel;
using KitRental.Web.Mvc.Models;
using KitRental.Web.Mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Web.Mvc.Controllers;

[Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff,ServiceTechnician,Auditor")]
public sealed class OperationsController(KitRentalApiClient apiClient) : Controller
{
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        var dashboard = await apiClient.GetOperationsDashboardAsync(cancellationToken)
            ?? new OperationsDashboardViewModel(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        return View(dashboard);
    }

    public async Task<IActionResult> Returns(CancellationToken cancellationToken)
    {
        return View(await apiClient.GetReturnsTableAsync(cancellationToken) ?? []);
    }

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
    public async Task<IActionResult> ReceiveReturn(Guid id, bool returnToReturns = false,
        CancellationToken cancellationToken = default)
    {
        var result = await apiClient.ReceiveKitReturnAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "İade teslim alındı; kitler yeniden kullanılabilir stoka eklendi."
            : result.Error ?? "İade teslim alınamadı.";
        return RedirectToAction(returnToReturns ? nameof(Returns) : nameof(Dashboard));
    }

    [HttpGet, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> ReturnKargonomiBarcode(Guid id, CancellationToken cancellationToken)
    {
        var result = await apiClient.GetReturnKargonomiBarcodeAsync(id, cancellationToken);
        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Data?.Base64))
            return NotFound(new { message = result.Error ?? "Henüz kargo barkodu oluşmamış, lütfen tekrar deneyin." });
        try
        {
            return File(Convert.FromBase64String(result.Data.Base64), "application/pdf", $"iade-kargonomi-{id:N}.pdf");
        }
        catch (FormatException)
        {
            return Problem("Kargonomi barkod PDF'i geçersiz döndü.");
        }
    }

    public async Task<IActionResult> Inventory(CancellationToken cancellationToken)
    {
        var allInventoryFilter = new InventoryFilterViewModel { Page = 1, PageSize = 5000 };
        var result = await apiClient.GetInventoryAsync(allInventoryFilter, cancellationToken)
            ?? new InventoryPageViewModel(1, allInventoryFilter.PageSize, 0, 1, []);
        return View(new InventoryScreenViewModel(result));
    }

    public async Task<IActionResult> Orders(int? type, CancellationToken cancellationToken)
    {
        var orders = await apiClient.GetOrdersAsync(cancellationToken);
        ViewBag.OrderType = type;
        return View(type is 1 or 2 ? orders.Where(item => item.Type == type).ToArray() : orders);
    }

    [HttpGet]
    public async Task<IActionResult> KargonomiShipments(CancellationToken cancellationToken) =>
        View(await apiClient.GetKargonomiShipmentsAsync(cancellationToken));

    [HttpGet]
    public async Task<IActionResult> OrderDetails(Guid id, bool edit = false, CancellationToken cancellationToken = default)
    {
        var model = await apiClient.GetOrderDetailAsync(id, cancellationToken);
        ViewData["OpenOrderPeriodDialog"] = edit;
        return model is null ? NotFound() : View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> UpdateOrderRentalPeriod(Guid id, OrderRentalPeriodInputViewModel model,
        bool returnToOrders = false, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid || model.EndDate <= model.StartDate)
        {
            TempData["Error"] = model.EndDate <= model.StartDate
                ? "Bitiş tarihi başlangıç tarihinden sonra olmalıdır."
                : "Dönem adı, başlangıç ve bitiş tarihi zorunludur.";
            return RedirectToAction(returnToOrders ? nameof(Orders) : nameof(OrderDetails),
                returnToOrders ? null : new { id });
        }

        var result = await apiClient.UpdateOrderRentalPeriodAsync(id, model, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Sipariş dönemi ve kiralama tarihleri güncellendi."
            : result.Error ?? "Sipariş dönemi güncellenemedi.";
        return RedirectToAction(returnToOrders ? nameof(Orders) : nameof(OrderDetails),
            returnToOrders ? null : new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> UpdateOrderStudent(Guid id, OrderStudentInputViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || model.StudentId == Guid.Empty)
        {
            TempData["Error"] = "Öğrenci adı soyadı ve geçerli telefon numarası zorunludur.";
            return RedirectToAction(nameof(OrderDetails), new { id });
        }

        var result = await apiClient.UpdateOrderStudentAsync(id, model, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Öğrenci bilgileri güncellendi."
            : result.Error ?? "Öğrenci bilgileri güncellenemedi.";
        return RedirectToAction(nameof(OrderDetails), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> AddOrderStudent(Guid id, OrderStudentInputViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Öğrenci adı soyadı ve geçerli telefon numarası zorunludur.";
            return RedirectToAction(nameof(OrderDetails), new { id });
        }

        var result = await apiClient.AddOrderStudentAsync(id, model, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Yeni öğrenci siparişe eklendi; hazırlanması gereken kit sayısı güncellendi."
            : result.Error ?? "Öğrenci siparişe eklenemedi.";
        return RedirectToAction(nameof(OrderDetails), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> StartKargonomiShipments(Guid id, Guid[]? studentIds,
        CancellationToken cancellationToken)
    {
        var result = await apiClient.StartKargonomiShipmentsAsync(id,
            studentIds is { Length: > 0 } ? studentIds : null, cancellationToken);
        if (!result.IsSuccess)
            TempData["Error"] = result.Error ?? "Kargonomi gönderileri başlatılamadı.";
        else
        {
            var message = $"{result.Data!.SucceededCount} gönderi başlatıldı, {result.Data.FailedCount} gönderi başarısız oldu.";
            var firstFailure = result.Data.Items.FirstOrDefault(item => !item.Succeeded)?.Message;
            TempData[result.Data.FailedCount == 0 ? "Success" : "Error"] = string.IsNullOrWhiteSpace(firstFailure)
                ? message
                : $"{message} İlk hata: {firstFailure}";
        }
        return RedirectToAction(nameof(OrderDetails), new { id });
    }

    [HttpGet, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> KargonomiBarcode(Guid id, Guid shipmentId,
        CancellationToken cancellationToken)
    {
        var result = await apiClient.GetKargonomiBarcodeAsync(shipmentId, cancellationToken);
        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Data?.Base64))
            return NotFound(new { message = "Henüz kargo etiketi oluşmamış, lütfen tekrar deneyin." });
        try
        {
            return File(Convert.FromBase64String(result.Data.Base64), "application/pdf", $"kargonomi-{shipmentId:N}.pdf");
        }
        catch (FormatException)
        {
            return Problem("Kargonomi barkod PDF'i geçersiz döndü.");
        }
    }

    [HttpGet, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> KargonomiBarcodes(Guid id, Guid[]? studentIds,
        CancellationToken cancellationToken)
    {
        var order = await apiClient.GetOrderDetailAsync(id, cancellationToken);
        if (order is null)
            return NotFound();

        var selectedStudentIds = (studentIds ?? [])
            .Where(studentId => studentId != Guid.Empty)
            .Distinct()
            .ToHashSet();
        var shipmentsByStudentId = order.KargonomiShipments
            .GroupBy(shipment => shipment.StudentId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(shipment => shipment.UpdatedAt).First());

        var items = await Task.WhenAll(order.Students
            .Where(student => selectedStudentIds.Contains(student.Id))
            .Select(async student =>
            {
                if (!shipmentsByStudentId.TryGetValue(student.Id, out var shipment) || !shipment.ExternalShipmentId.HasValue)
                    return new KargonomiBarcodePrintItemViewModel(student.FullName, null,
                        "Henüz kargo etiketi oluşmamış, lütfen tekrar deneyin.");

                var result = await apiClient.GetKargonomiBarcodeAsync(shipment.Id, cancellationToken);
                return result.IsSuccess && !string.IsNullOrWhiteSpace(result.Data?.Base64)
                    ? new KargonomiBarcodePrintItemViewModel(student.FullName, result.Data.Base64, null)
                    : new KargonomiBarcodePrintItemViewModel(student.FullName, null,
                        "Henüz kargo etiketi oluşmamış, lütfen tekrar deneyin.");
            }));

        return View("KargonomiBarcodes", new KargonomiBarcodePrintPageViewModel(items));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> DeleteOrderStudent(Guid id, Guid studentId, CancellationToken cancellationToken)
    {
        var result = await apiClient.DeleteOrderStudentAsync(id, studentId, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Öğrenci ve sipariş içindeki fiziksel kit bağlantısı silindi."
            : result.Error ?? "Öğrenci silinemedi.";
        return RedirectToAction(nameof(OrderDetails), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> DeleteOrderStudents(Guid id, Guid[]? studentIds, CancellationToken cancellationToken)
    {
        var uniqueStudentIds = (studentIds ?? []).Where(studentId => studentId != Guid.Empty).Distinct().ToArray();
        if (uniqueStudentIds.Length == 0)
        {
            TempData["Error"] = "Silmek için en az bir öğrenci seçin.";
            return RedirectToAction(nameof(OrderDetails), new { id });
        }

        var deletedCount = 0;
        string? firstError = null;
        foreach (var studentId in uniqueStudentIds)
        {
            var result = await apiClient.DeleteOrderStudentAsync(id, studentId, cancellationToken);
            if (result.IsSuccess)
                deletedCount++;
            else
                firstError ??= result.Error;
        }

        var failedCount = uniqueStudentIds.Length - deletedCount;
        if (failedCount == 0)
            TempData["Success"] = $"{deletedCount} öğrenci ve sipariş içindeki fiziksel kit bağlantıları silindi.";
        else
            TempData["Error"] = $"{deletedCount} öğrenci silindi, {failedCount} öğrenci silinemedi. {firstError ?? "İşlem tamamlanamadı."}";

        return RedirectToAction(nameof(OrderDetails), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> ConfirmStudentDelivery(Guid id, Guid studentId,
        CancellationToken cancellationToken)
    {
        var result = await apiClient.ConfirmStudentDeliveryAsync(id, studentId, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Öğrenciye teslim edildi olarak işaretlendi; kit konumu güncellendi."
            : result.Error ?? "Öğrenci teslimi işaretlenemedi.";
        return RedirectToAction(nameof(OrderDetails), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> ConfirmStudentDeliveries(Guid id, Guid[] studentIds,
        CancellationToken cancellationToken)
    {
        var result = await apiClient.ConfirmStudentDeliveriesAsync(id, studentIds, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"{studentIds.Length} öğrenci teslim edildi olarak işaretlendi; kit konumları güncellendi."
            : result.Error ?? "Öğrenciler teslim edildi olarak işaretlenemedi.";
        return RedirectToAction(nameof(OrderDetails), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> ExportOrderStudents(Guid id, Guid[]? studentIds,
        CancellationToken cancellationToken)
    {
        var order = await apiClient.GetOrderDetailAsync(id, cancellationToken);
        if (order is null) return NotFound();
        var selectedStudentIds = (studentIds ?? []).Where(studentId => studentId != Guid.Empty).Distinct().ToHashSet();
        var students = selectedStudentIds.Count == 0
            ? order.Students
            : order.Students.Where(student => selectedStudentIds.Contains(student.Id)).ToArray();
        return ExportStudentAddressWorkbook(order.OrderNumber, students.Select(student =>
            new StudentAddressExportRow(student.FullName, student.GuardianPhone,
                string.IsNullOrWhiteSpace(student.ProductName) ? "Eğitim kiti" : student.ProductName, student.HasAddress,
                student.AddressLine, BuildStudentAddressUrl(student.PublicAddressToken))).ToArray());
    }

    [HttpGet]
    public async Task<IActionResult> ExportKargonomi(Guid id, Guid[]? studentIds,
        CancellationToken cancellationToken)
    {
        var order = await apiClient.GetOrderDetailAsync(id, cancellationToken);
        if (order is null) return NotFound();

        var selectedStudentIds = (studentIds ?? []).Where(studentId => studentId != Guid.Empty).Distinct().ToHashSet();
        var students = selectedStudentIds.Count == 0
            ? order.Students
            : order.Students.Where(student => selectedStudentIds.Contains(student.Id)).ToArray();
        return ExportKargonomiWorkbook(order.OrderNumber, students.Where(student => student.HasAddress).Select(student =>
        {
            var (city, district) = ParseStudentAddressRegion(student.AddressLine);
            return new KargonomiExportRow(student.FullName, student.AddressLine, city, district,
                student.GuardianPhone, string.IsNullOrWhiteSpace(student.ProductName) ? "Eğitim kiti" : student.ProductName);
        }).ToArray());
    }

    [HttpGet, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> CreateOrder(CancellationToken cancellationToken)
    {
        var customers = (await apiClient.GetCustomersAsync(cancellationToken)).Where(item => item.IsActive).ToArray();
        var model = new AdminOrderInputViewModel
        {
            CustomerId = customers.FirstOrDefault()?.Id ?? Guid.Empty,
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
        model.Students ??= [];
        model.Students = model.Students
            .Where(student => !string.IsNullOrWhiteSpace(student.FullName) ||
                !string.IsNullOrWhiteSpace(student.GuardianPhone))
            .ToList();
        if (model.ProductModelId == Guid.Empty)
            ModelState.AddModelError(nameof(model.ProductModelId), "Eğitim kiti seçmelisiniz.");
        if (model.Students.Count == 0)
            ModelState.AddModelError(string.Empty, "En az bir öğrenci girmelisiniz.");
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
        if (model.Students.Count == 0)
            model.Students.Add(new AdminOrderStudentInputViewModel());
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
    public async Task<IActionResult> FaultGuide(Guid? productModelId, Guid? editId, CancellationToken cancellationToken)
    {
        var allEntries = await apiClient.GetFaultGuideEntriesAsync(cancellationToken);
        var productModels = await apiClient.GetProductModelsAsync(cancellationToken);
        var edit = editId.HasValue ? allEntries.SingleOrDefault(item => item.Id == editId.Value) : null;
        var selectedProductModelId = edit?.ProductModelId ?? productModelId;
        var entries = selectedProductModelId.HasValue
            ? allEntries.Where(item => item.ProductModelId == selectedProductModelId).ToArray()
            : [];
        var form = edit is null
            ? new FaultGuideEntryInputViewModel
            {
                ProductModelId = selectedProductModelId,
                DisplayOrder = entries.Length == 0 ? 10 : entries.Max(item => item.DisplayOrder) + 10
            }
            : new FaultGuideEntryInputViewModel
            {
                Id = edit.Id,
                ProductModelId = edit.ProductModelId,
                Title = edit.Title,
                Problem = edit.Problem,
                Solution = edit.Solution,
                DisplayOrder = edit.DisplayOrder,
                IsActive = edit.IsActive
            };
        return View(new FaultGuidePageViewModel(entries, form, productModels, selectedProductModelId));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> SaveFaultGuide(FaultGuideEntryInputViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View("FaultGuide", new FaultGuidePageViewModel(
                (await apiClient.GetFaultGuideEntriesAsync(cancellationToken))
                    .Where(item => item.ProductModelId == model.ProductModelId).ToArray(), model,
                await apiClient.GetProductModelsAsync(cancellationToken), model.ProductModelId));
        var result = model.Id.HasValue
            ? await apiClient.UpdateFaultGuideEntryAsync(model, cancellationToken)
            : await apiClient.CreateFaultGuideEntryAsync(model, cancellationToken);
        if (result.IsSuccess)
        {
            TempData["Success"] = model.Id.HasValue
                ? "Problem rehberi guncellendi."
                : "Problem rehberi eklendi.";
            return RedirectToAction(nameof(FaultGuide), new { productModelId = model.ProductModelId });
        }
        ModelState.AddModelError(string.Empty, result.Error ?? "Problem rehberi kaydedilemedi.");
        return View("FaultGuide", new FaultGuidePageViewModel(
            (await apiClient.GetFaultGuideEntriesAsync(cancellationToken))
                .Where(item => item.ProductModelId == model.ProductModelId).ToArray(), model,
            await apiClient.GetProductModelsAsync(cancellationToken), model.ProductModelId));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> DeleteFaultGuide(Guid id, Guid? productModelId, CancellationToken cancellationToken)
    {
        var result = await apiClient.DeleteFaultGuideEntryAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Problem rehberi silindi."
            : result.Error ?? "Problem rehberi silinemedi.";
        return RedirectToAction(nameof(FaultGuide), new { productModelId });
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
    public async Task<IActionResult> CreateSelectedOrderKits(Guid id, Guid[] studentIds,
        CancellationToken cancellationToken)
    {
        if (studentIds.Length == 0)
        {
            TempData["Error"] = "Kit oluşturmak için en az bir öğrenci seçmelisiniz.";
            return RedirectToAction(nameof(OrderDetails), new { id });
        }

        var order = await apiClient.GetOrderDetailAsync(id, cancellationToken);
        if (order is null) return NotFound();
        var result = await apiClient.CreateOrderKitsAsync(id, [], true, order.RentalCohortId,
            cancellationToken, studentIds);
        if (result.IsSuccess)
        {
            var data = result.Data!;
            TempData["Success"] = data.ReusedCount > 0
                ? $"Seçilen öğrenciler için {data.ReusedCount} hazır kit rezerve edildi; {data.CreatedCount} fiziksel kit üretildi."
                : $"Seçilen öğrenciler için {data.CreatedCount} fiziksel kit oluşturuldu ve rezerve edildi.";
        }
        else
        {
            TempData["Error"] = result.Error ?? "Seçilen öğrenciler için fiziksel kit oluşturulamadı.";
        }
        return RedirectToAction(nameof(OrderDetails), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SystemAdmin,OperationsManager")]
    public async Task<IActionResult> UpdateOrderStatus(Guid id, int target, bool returnToDetails,
        CancellationToken cancellationToken)
    {
        if (target is not (3 or 13))
            return BadRequest();
        var result = await apiClient.UpdateOrderStatusAsync(id, target, cancellationToken);
        if (result.IsSuccess)
        {
            var statusName = target switch
            {
                3 => "onaylandı",
                13 => "tamamlandı",
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
    public async Task<IActionResult> UpdateFault(Guid id, int status, string? note, CancellationToken cancellationToken)
    {
        var result = await apiClient.ChangeFaultStatusAsync(id, status, note, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Arıza süreci güncellendi; müşteri portalına yansıtıldı." : result.Error;
        return RedirectToAction(nameof(Faults));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "SystemAdmin,OperationsManager,ServiceTechnician")]
    public async Task<IActionResult> StartFaultKargonomiShipment(Guid id, int direction, string recipientName,
        string recipientPhone, string recipientAddress, CancellationToken cancellationToken)
    {
        var result = await apiClient.StartFaultKargonomiShipmentAsync(id, direction, recipientName,
            recipientPhone, recipientAddress, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Arıza Kargonomi gönderisi başlatıldı." : result.Error;
        return RedirectToAction(nameof(Faults));
    }

    private string BuildStudentAddressUrl(string token) =>
        Url.Action("Index", "PublicStudentAddress", new { token }, Request.Scheme) ?? string.Empty;

    private FileContentResult ExportStudentAddressWorkbook(string orderNumber,
        IReadOnlyCollection<StudentAddressExportRow> students)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Öğrenci Adresleri");
        sheet.Cell(1, 1).Value = "Öğrenci Adı Soyadı";
        sheet.Cell(1, 2).Value = "Telefon Numarası";
        sheet.Cell(1, 3).Value = "Eğitim Kiti";
        sheet.Cell(1, 4).Value = "Adres Durumu";
        sheet.Cell(1, 5).Value = "Adres";
        sheet.Cell(1, 6).Value = "Public Link";
        sheet.Row(1).Style.Font.Bold = true;
        var rowIndex = 2;
        foreach (var student in students)
        {
            sheet.Cell(rowIndex, 1).Value = student.FullName;
            sheet.Cell(rowIndex, 2).Value = student.Phone;
            sheet.Cell(rowIndex, 3).Value = student.ProductName;
            sheet.Cell(rowIndex, 4).Value = student.HasAddress ? "Tamamlandı" : "Bekleniyor";
            sheet.Cell(rowIndex, 5).Value = student.AddressLine;
            sheet.Cell(rowIndex, 6).Value = student.PublicLink;
            rowIndex++;
        }
        sheet.Columns().AdjustToContents();
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return File(output.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"{SafeFileName(orderNumber)}-ogrenci-adresleri.xlsx");
    }

    private FileContentResult ExportKargonomiWorkbook(string orderNumber,
        IReadOnlyCollection<KargonomiExportRow> students)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Kargonomi");
        var headers = new[]
        {
            "Gönderici Ad Soyad*", "Gönderici Adres*", "Gönderici İl*", "Gönderici İlçe*",
            "Gönderici Mahalle*", "Gönderici Cep Telefonu*", "Gönderici Eposta Adresi*",
            "Gönderici Vergi Dairesi*", "Gönderici Vergi No/TC Kimlik No*", "Alıcı Ad Soyad*",
            "Alıcı Adres*", "Alıcı İl*", "Alıcı İlçe*", "Alıcı Mahalle", "Alıcı Cep Telefonu*",
            "Alıcı Eposta Adresi*", "Sipariş Tutarı", "1.Paket Desi/Ağırlık*", "2.Paket Desi/Ağırlık",
            "3.Paket Desi/Ağırlık", "4.Paket Desi/Ağırlık", "5.Paket Desi/Ağırlık", "İçerik", "Mail"
        };
        for (var column = 0; column < headers.Length; column++)
            sheet.Cell(1, column + 1).Value = headers[column];
        sheet.Row(1).Style.Font.Bold = true;

        var rowIndex = 2;
        foreach (var student in students)
        {
            var values = new object?[]
            {
                "Robotik Bilim", "ESKİ LONDRA ASFALTI CADDESİ YTÜ TEKNOPARK C1 106", "İSTANBUL",
                "ESENLER", "ÇİFTEHAVUZLAR", "5536589698", "hasan@robotikbilim.com.tr", "BAŞAKŞEHİR",
                "7721701834", student.FullName, student.AddressLine, student.City, student.District, null,
                student.Phone, "admin@robotikbilim.com.tr", null, 2, null, null, null, null, student.Content, null
            };
            for (var column = 0; column < values.Length; column++)
                sheet.Cell(rowIndex, column + 1).Value = values[column]?.ToString() ?? string.Empty;
            rowIndex++;
        }

        sheet.Columns().AdjustToContents();
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return File(output.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"{SafeFileName(orderNumber)}-kargonomi.xlsx");
    }

    private static (string City, string District) ParseStudentAddressRegion(string? addressLine)
    {
        if (string.IsNullOrWhiteSpace(addressLine)) return (string.Empty, string.Empty);
        var separatorIndex = addressLine.IndexOf(" - ", StringComparison.Ordinal);
        if (separatorIndex <= 0) return (string.Empty, string.Empty);
        var region = addressLine[..separatorIndex];
        var slashIndex = region.IndexOf('/', StringComparison.Ordinal);
        if (slashIndex <= 0 || slashIndex == region.Length - 1) return (string.Empty, string.Empty);
        return (region[..slashIndex].Trim(), region[(slashIndex + 1)..].Trim());
    }

    private static string SafeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(character =>
            invalidChars.Contains(character) ? '-' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "ogrenci-adresleri" : cleaned;
    }

    private sealed record StudentAddressExportRow(string FullName, string Phone, string ProductName,
        bool HasAddress, string AddressLine, string PublicLink);

    private sealed record KargonomiExportRow(string FullName, string AddressLine, string City,
        string District, string Phone, string Content);
}
