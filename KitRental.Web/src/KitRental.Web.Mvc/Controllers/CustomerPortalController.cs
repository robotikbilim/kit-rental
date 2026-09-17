using ClosedXML.Excel;
using KitRental.SharedKernel;
using KitRental.Web.Mvc.Models;
using KitRental.Web.Mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace KitRental.Web.Mvc.Controllers;

[Authorize(Roles = "CustomerAccountManager,CustomerUser")]
public sealed class CustomerPortalController(KitRentalApiClient apiClient) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        return portal is null ? Forbid() : View(portal);
    }

    [HttpGet]
    public IActionResult Orders() => RedirectToAction(nameof(RentalPeriods));

    [HttpGet]
    public async Task<IActionResult> RentalPeriods(string? periodName, string? approvalStatus, int page = 1,
        CancellationToken cancellationToken = default)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        if (portal is null) return Forbid();
        return View(BuildRentalCohortsPage(portal, new RentalCohortInputViewModel
        {
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(1))
        }, periodName, approvalStatus, page));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRentalPeriod(RentalCohortInputViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.EndDate <= model.StartDate)
            ModelState.AddModelError(nameof(model.EndDate), "Bitiş tarihi başlangıç tarihinden sonra olmalıdır.");
        if (ModelState.IsValid)
        {
            var result = model.Id.HasValue
                ? await apiClient.UpdateRentalCohortAsync(model, cancellationToken)
                : await apiClient.CreateRentalCohortAsync(model, cancellationToken);
            if (result.IsSuccess)
            {
                TempData["Success"] = "Kiralama dönemi kaydedildi.";
                return RedirectToAction(nameof(RentalPeriods));
            }
            ModelState.AddModelError(string.Empty, result.Error ?? "Kiralama dönemi kaydedilemedi.");
        }
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        return portal is null ? Forbid() : View("RentalPeriods", BuildRentalCohortsPage(portal, model));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRentalPeriod(Guid id, CancellationToken cancellationToken)
    {
        var result = await apiClient.DeleteRentalCohortAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Sipariş silindi."
            : result.Error ?? "Sipariş silinemedi.";
        return RedirectToAction(nameof(RentalPeriods));
    }

    private static RentalCohortsPageViewModel BuildRentalCohortsPage(CustomerPortalViewModel portal,
        RentalCohortInputViewModel form, string? periodName = null, string? approvalStatus = null, int page = 1)
    {
        var periodNameOptions = portal.RentalCohorts
            .Select(item => item.Name.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(name => name)
            .ToArray();
        var normalizedPeriodName = string.IsNullOrWhiteSpace(periodName) ? null : periodName.Trim();
        var normalizedApprovalStatus = NormalizeRentalPeriodApprovalStatus(approvalStatus);
        var filtered = portal.RentalCohorts.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(normalizedPeriodName))
        {
            filtered = filtered.Where(item => string.Equals(item.Name.Trim(), normalizedPeriodName,
                StringComparison.CurrentCultureIgnoreCase));
        }
        filtered = normalizedApprovalStatus switch
        {
            "not-created" => filtered.Where(item => !item.OrderStatus.HasValue),
            "unapproved" => filtered.Where(item => item.OrderStatus is 2 or 14 or 15),
            "approved" => filtered.Where(item => item.OrderStatus.HasValue && item.OrderStatus is not 2 and not 14 and not 15),
            _ => filtered
        };

        var filteredList = filtered
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.Name)
            .ToArray();
        var totalCount = filteredList.Length;

        return new RentalCohortsPageViewModel(portal.CustomerName, filteredList, form, periodNameOptions,
            normalizedPeriodName, normalizedApprovalStatus, 1, Math.Max(10, totalCount), totalCount);
    }

    private static string? NormalizeRentalPeriodApprovalStatus(string? approvalStatus)
    {
        if (string.IsNullOrWhiteSpace(approvalStatus)) return null;
        return approvalStatus.Trim().ToLowerInvariant() switch
        {
            "not-created" => "not-created",
            "unapproved" => "unapproved",
            "approved" => "approved",
            _ => null
        };
    }

    [HttpGet]
    public async Task<IActionResult> RentalPeriod(Guid id, Guid? editStudentId, string? studentQuery,
        Guid? productModelId, string? assignmentState, string? addressState, int page = 1,
        CancellationToken cancellationToken = default)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        if (portal is null) return Forbid();
        var cohort = portal.RentalCohorts.SingleOrDefault(item => item.Id == id);
        if (cohort is null) return NotFound();
        var edit = editStudentId.HasValue
            ? cohort.Students.SingleOrDefault(item => item.Id == editStudentId.Value)
            : null;
        var form = edit is null
            ? new RentalCohortStudentInputViewModel { CohortId = id }
            : new RentalCohortStudentInputViewModel
            {
                Id = edit.Id,
                CohortId = id,
                FullName = edit.FullName,
                GuardianPhone = edit.GuardianPhone,
                AddressLine = edit.AddressLine,
                ProductModelId = edit.ProductModelId
            };
        var allStudents = cohort.Students
            .OrderBy(student => student.FullName)
            .ThenBy(student => student.GuardianPhone)
            .ToArray();
        return View(new RentalCohortDetailPageViewModel(cohort, form, portal.ProductModels, allStudents,
            null, null, null, null, 1, Math.Max(1, allStudents.Length), allStudents.Length));
    }

    [HttpGet]
    public async Task<IActionResult> ExportRentalPeriodStudents(Guid id, CancellationToken cancellationToken)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        if (portal is null) return Forbid();
        var cohort = portal.RentalCohorts.SingleOrDefault(item => item.Id == id);
        if (cohort is null) return NotFound();

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Öğrenci Adresleri");
        sheet.Cell(1, 1).Value = "Öğrenci Adı Soyadı";
        sheet.Cell(1, 2).Value = "Telefon Numarası";
        sheet.Cell(1, 3).Value = "Eğitim Kiti";
        sheet.Cell(1, 4).Value = "Adres Durumu";
        sheet.Cell(1, 5).Value = "Adres";
        sheet.Cell(1, 6).Value = "Public Link";
        sheet.Cell(1, 7).Value = "Atanan Fiziksel Kit QR Linki";
        sheet.Row(1).Style.Font.Bold = true;
        var rowIndex = 2;
        foreach (var student in cohort.Students.OrderBy(item => item.FullName))
        {
            sheet.Cell(rowIndex, 1).Value = student.FullName;
            sheet.Cell(rowIndex, 2).Value = student.GuardianPhone;
            sheet.Cell(rowIndex, 3).Value = student.ProductModelName;
            sheet.Cell(rowIndex, 4).Value = string.IsNullOrWhiteSpace(student.AddressLine)
                ? "Bekleniyor"
                : "Tamamlandı";
            sheet.Cell(rowIndex, 5).Value = student.AddressLine;
            sheet.Cell(rowIndex, 6).Value = BuildStudentAddressUrl(student.PublicAddressToken);
            sheet.Cell(rowIndex, 7).Value = BuildPhysicalKitQrUrl(student.QrCode);
            rowIndex++;
        }
        sheet.Columns().AdjustToContents();
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return File(output.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"{SafeFileName(cohort.OrderNumber ?? cohort.Name)}-ogrenci-adresleri.xlsx");
    }

    private static string? NormalizeStudentAssignmentState(string? assignmentState)
    {
        if (string.IsNullOrWhiteSpace(assignmentState)) return null;
        return assignmentState.Trim().ToLowerInvariant() switch
        {
            "assigned" => "assigned",
            "unassigned" => "unassigned",
            "returning" => "returning",
            "delivered" => "delivered",
            _ => null
        };
    }

    private static string? NormalizeStudentAddressState(string? addressState)
    {
        if (string.IsNullOrWhiteSpace(addressState)) return null;
        return addressState.Trim().ToLowerInvariant() switch
        {
            "completed" => "completed",
            "pending" => "pending",
            _ => null
        };
    }

    private string BuildStudentAddressUrl(string token) =>
        Url.Action("Index", "PublicStudentAddress", new { token }, Request.Scheme) ?? string.Empty;

    private string BuildPhysicalKitQrUrl(string? qrCode) =>
        string.IsNullOrWhiteSpace(qrCode)
            ? string.Empty
            : Url.Action("Index", "PublicFault", new { qrCode }, Request.Scheme) ?? string.Empty;

    private static string SafeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(character =>
            invalidChars.Contains(character) ? '-' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "ogrenci-adresleri" : cleaned;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRentalPeriodStudent(RentalCohortStudentInputViewModel model,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = model.Id.HasValue
                ? await apiClient.UpdateRentalCohortStudentAsync(model, cancellationToken)
                : await apiClient.CreateRentalCohortStudentAsync(model, cancellationToken);
            if (result.IsSuccess)
            {
                TempData["Success"] = "Öğrenci kaydedildi. Sipariş admin onayında görünür ve onaya kadar öğrenci listesi düzenlenebilir.";
                return RedirectToAction(nameof(RentalPeriod), new { id = model.CohortId });
            }
            ModelState.AddModelError(string.Empty, result.Error ?? "Öğrenci kaydedilemedi.");
        }
        return await RentalPeriod(model.CohortId, model.Id, null, null, null, null,
            cancellationToken: cancellationToken);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRentalPeriodStudent(Guid cohortId, Guid studentId,
        CancellationToken cancellationToken)
    {
        var result = await apiClient.DeleteRentalCohortStudentAsync(cohortId, studentId, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Öğrenci listeden kaldırıldı."
            : result.Error ?? "Öğrenci kaldırılamadı.";
        return RedirectToAction(nameof(RentalPeriod), new { id = cohortId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRentalPeriodStudents(Guid cohortId, Guid[]? studentIds,
        CancellationToken cancellationToken)
    {
        var uniqueStudentIds = (studentIds ?? []).Where(studentId => studentId != Guid.Empty).Distinct().ToArray();
        if (uniqueStudentIds.Length == 0)
        {
            TempData["Error"] = "Silmek için en az bir öğrenci seçin.";
            return RedirectToAction(nameof(RentalPeriod), new { id = cohortId });
        }

        var deletedCount = 0;
        string? firstError = null;
        foreach (var studentId in uniqueStudentIds)
        {
            var result = await apiClient.DeleteRentalCohortStudentAsync(cohortId, studentId, cancellationToken);
            if (result.IsSuccess)
                deletedCount++;
            else
                firstError ??= result.Error;
        }

        var failedCount = uniqueStudentIds.Length - deletedCount;
        if (failedCount == 0)
            TempData["Success"] = $"{deletedCount} öğrenci listeden kaldırıldı.";
        else
            TempData["Error"] = $"{deletedCount} öğrenci silindi, {failedCount} öğrenci silinemedi. {firstError ?? "İşlem tamamlanamadı."}";

        return RedirectToAction(nameof(RentalPeriod), new { id = cohortId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportRentalPeriodStudents(Guid cohortId, Guid productModelId, IFormFile? file,
        CancellationToken cancellationToken)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        if (portal is null) return Forbid();
        var cohort = portal.RentalCohorts.SingleOrDefault(item => item.Id == cohortId);
        if (cohort is null) return NotFound();
        if (cohort.IsApproved)
        {
            TempData["Error"] = "Onaylanmış siparişlerde öğrenci listesi kilitlidir; yeni öğrenci yüklenemez.";
            return RedirectToAction(nameof(RentalPeriod), new { id = cohortId });
        }
        if (portal.ProductModels.All(item => item.Id != productModelId))
        {
            TempData["Error"] = "Yüklenen liste için eğitim kiti seçilmelidir.";
            return RedirectToAction(nameof(RentalPeriod), new { id = cohortId });
        }
        if (file is null || file.Length == 0)
        {
            TempData["Error"] = "Excel dosyası seçilmelidir.";
            return RedirectToAction(nameof(RentalPeriod), new { id = cohortId });
        }
        var rows = new List<RentalCohortStudentImportPreviewRowViewModel>();
        await using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();
        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var fullName = row.Cell(1).GetString().Trim();
            var phone = row.Cell(2).GetString().Trim();
            if (string.IsNullOrWhiteSpace(fullName) && string.IsNullOrWhiteSpace(phone))
                continue;
            rows.Add(new RentalCohortStudentImportPreviewRowViewModel
            {
                FullName = fullName,
                GuardianPhone = phone,
                AddressLine = string.Empty,
                ProductModelId = productModelId
            });
        }
        if (rows.Count == 0)
        {
            TempData["Error"] = "Excel dosyasında içe aktarılacak öğrenci bulunamadı.";
            return RedirectToAction(nameof(RentalPeriod), new { id = cohortId });
        }
        return View("RentalPeriodImportPreview", new RentalCohortStudentImportPreviewViewModel
        {
            CohortId = cohortId,
            CohortName = cohort.Name,
            Rows = rows,
            ProductModels = portal.ProductModels
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmRentalPeriodStudentImport(
        RentalCohortStudentImportPreviewViewModel model, CancellationToken cancellationToken)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        if (portal is null) return Forbid();
        var cohort = portal.RentalCohorts.SingleOrDefault(item => item.Id == model.CohortId);
        if (cohort is null) return NotFound();
        if (cohort.IsApproved)
        {
            TempData["Error"] = "Onaylanmış siparişlerde öğrenci listesi kilitlidir; içe aktarma onaylanamaz.";
            return RedirectToAction(nameof(RentalPeriod), new { id = model.CohortId });
        }
        model.ProductModels = portal.ProductModels;
        model.CohortName = cohort.Name;
        model.Rows = model.Rows
            .Where(row => !string.IsNullOrWhiteSpace(row.FullName) ||
                !string.IsNullOrWhiteSpace(row.GuardianPhone) ||
                row.ProductModelId != Guid.Empty)
            .ToList();
        var modelIds = portal.ProductModels.Select(item => item.Id).ToHashSet();
        if (model.Rows.Count == 0)
            ModelState.AddModelError(string.Empty, "İçe aktarılacak öğrenci satırı bulunamadı.");
        for (var index = 0; index < model.Rows.Count; index++)
        {
            var row = model.Rows[index];
            if (string.IsNullOrWhiteSpace(row.FullName))
                ModelState.AddModelError($"Rows[{index}].FullName", "Öğrenci adı soyadı zorunludur.");
            if (string.IsNullOrWhiteSpace(row.GuardianPhone))
                ModelState.AddModelError($"Rows[{index}].GuardianPhone", "Veli telefon numarası zorunludur.");
            else if (!TurkishPhoneNumber.IsValid(row.GuardianPhone))
                ModelState.AddModelError($"Rows[{index}].GuardianPhone", "Veli telefon numarası 0xxx xxx xx xx formatında olmalıdır.");
            if (!modelIds.Contains(row.ProductModelId))
                ModelState.AddModelError($"Rows[{index}].ProductModelId", "Her satır için eğitim kiti seçilmelidir.");
        }
        if (!ModelState.IsValid)
            return View("RentalPeriodImportPreview", model);
        var rows = model.Rows.Select(row => new
        {
            fullName = row.FullName,
            guardianPhone = TurkishPhoneNumber.Normalize(row.GuardianPhone, "Veli telefon numarası"),
            addressLine = row.AddressLine ?? string.Empty,
            productModel = row.ProductModelId.ToString()
        }).ToArray();
        var result = await apiClient.ImportRentalCohortStudentsAsync(model.CohortId, rows, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"{rows.Length} öğrenci içe aktarıldı. Sipariş admin onayında görünür ve onaya kadar öğrenci listesi düzenlenebilir."
            : result.Error ?? "Öğrenci listesi içe aktarılamadı.";
        return RedirectToAction(nameof(RentalPeriod), new { id = model.CohortId });
    }

    [HttpGet]
    public async Task<IActionResult> RentalPeriodTemplate(CancellationToken cancellationToken)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        if (portal is null) return Forbid();
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Öğrenciler");
        sheet.Cell(1, 1).Value = "Öğrenci Adı Soyadı";
        sheet.Cell(1, 2).Value = "Veli Telefon Numarası";
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Columns(1, 2).AdjustToContents();
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return File(output.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "tacev-ogrenci-listesi-sablonu.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> StartStudentReturn(Guid cohortId, Guid studentId,
        CancellationToken cancellationToken)
    {
        var model = await BuildStudentReturnFormAsync(cohortId, studentId, cancellationToken);
        return model is null ? NotFound() : View("StudentReturn", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> StartStudentReturn(PortalStudentReturnFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await apiClient.CreateStudentReturnAsync(model, cancellationToken);
            if (result.IsSuccess)
            {
                TempData["Success"] = "İade süreci başlatıldı.";
                return RedirectToAction(nameof(RentalPeriod), new { id = model.CohortId });
            }

            ModelState.AddModelError(string.Empty, result.Error ?? "İade süreci başlatılamadı.");
        }

        var rebuilt = await BuildStudentReturnFormAsync(model.CohortId, model.StudentId, cancellationToken);
        if (rebuilt is null) return NotFound();
        model.StudentName = rebuilt.StudentName;
        model.KitName = rebuilt.KitName;
        model.SerialNumber = rebuilt.SerialNumber;
        return View("StudentReturn", model);
    }

    private async Task<PortalStudentReturnFormViewModel?> BuildStudentReturnFormAsync(Guid cohortId, Guid studentId,
        CancellationToken cancellationToken)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        var cohort = portal?.RentalCohorts.SingleOrDefault(item => item.Id == cohortId);
        var student = cohort?.Students.SingleOrDefault(item => item.Id == studentId);
        if (student is null || !student.AssignmentId.HasValue || student.HasActiveReturn || student.HasCompletedReturn)
            return null;

        return new PortalStudentReturnFormViewModel
        {
            CohortId = cohortId,
            StudentId = studentId,
            StudentName = student.FullName,
            KitName = student.ProductModelName,
            SerialNumber = student.SerialNumber ?? "-",
            RequesterName = string.IsNullOrWhiteSpace(student.DeliveredTo) ? student.FullName : student.DeliveredTo,
            RequesterPhone = string.IsNullOrWhiteSpace(student.DeliveryPhone) ? student.GuardianPhone : student.DeliveryPhone,
            ReturnAddress = string.IsNullOrWhiteSpace(student.DeliveryAddress) ? student.AddressLine : student.DeliveryAddress
        };
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRentalPeriodOrder(Guid cohortId, CancellationToken cancellationToken)
    {
        var result = await apiClient.CreateRentalCohortOrderAsync(cohortId, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"Sipariş oluşturuldu: {result.Data!.OrderNumber}. Admin onayından sonra fiziksel kitler hazırlanabilir."
            : result.Error ?? "Sipariş oluşturulamadı.";
        return RedirectToAction(nameof(RentalPeriod), new { id = cohortId });
    }

    [HttpGet]
    public async Task<IActionResult> Faults(string? query, int? status, string state = "all", int page = 1,
        int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        if (portal is null) return Forbid();

        var normalizedQuery = query?.Trim() ?? string.Empty;
        var normalizedStatus = status is >= 1 and <= 8 ? status : null;
        var normalizedState = state is "open" or "completed" ? state : "all";
        var allFaults = portal.Faults
            .OrderByDescending(item => item.OpenedAt)
            .ThenBy(item => item.Number)
            .ToArray();

        IEnumerable<PortalFaultViewModel> filteredFaults = allFaults;
        if (normalizedQuery.Length > 0)
        {
            filteredFaults = filteredFaults.Where(item =>
                item.Number.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                item.KitName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                item.SerialNumber.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                item.Category.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                item.Description.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase));
        }
        if (normalizedStatus.HasValue)
            filteredFaults = filteredFaults.Where(item => item.Status == normalizedStatus.Value);
        if (normalizedState == "open")
            filteredFaults = filteredFaults.Where(item => item.Status is not (7 or 8));
        if (normalizedState == "completed")
            filteredFaults = filteredFaults.Where(item => item.Status is 7 or 8);

        var filtered = filteredFaults.ToArray();
        return View(new PortalFaultsPageViewModel(portal.CustomerName, normalizedQuery, normalizedStatus,
            normalizedState, 1, Math.Max(10, filtered.Length), filtered.Length, allFaults.Length, filtered));
    }

    [HttpGet]
    public async Task<IActionResult> Kits(string? query, int? status, bool? hasFault, bool? deliveryFormMissing,
        string? assignmentState = "all",
        int page = 1,
        int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        if (portal is null) return Forbid();

        var normalizedQuery = query?.Trim() ?? string.Empty;
        var normalizedStatus = status is >= 1 and <= 8 ? status : null;
        var requestedAssignmentState = assignmentState?.Trim().ToLowerInvariant();
        var normalizedAssignmentState = requestedAssignmentState is "assigned" or "unassigned"
            ? requestedAssignmentState
            : "all";
        var normalizedPageSize = pageSize is 10 or 25 or 50 ? pageSize : 10;
        var allKits = portal.Kits
            .Where(item => item.AssignmentStatus is 1 or 2)
            .OrderByDescending(item => item.StartDate)
            .ThenBy(item => item.KitName)
            .ThenBy(item => item.SerialNumber)
            .ToArray();

        IEnumerable<PortalKitViewModel> filteredKits = allKits;
        if (normalizedQuery.Length > 0)
        {
            filteredKits = filteredKits.Where(item =>
                item.KitName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                item.KitSku.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                item.SerialNumber.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                item.OrderNumber.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                (item.AssignedStudentName?.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                (item.AssignedStudentGuardianPhone?.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                (item.AssignedStudentAddressLine?.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                (item.AssignedStudentPeriodName?.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase) ?? false));
        }
        if (normalizedStatus.HasValue)
            filteredKits = filteredKits.Where(item => item.UnitStatus == normalizedStatus.Value);
        if (hasFault.HasValue)
            filteredKits = filteredKits.Where(item => (item.OpenFaultCount > 0) == hasFault.Value);
        if (deliveryFormMissing.HasValue)
            filteredKits = filteredKits.Where(item => item.HasDeliveryForm != deliveryFormMissing.Value);
        filteredKits = normalizedAssignmentState switch
        {
            "assigned" => filteredKits.Where(item => !string.IsNullOrWhiteSpace(item.AssignedStudentName)),
            "unassigned" => filteredKits.Where(item => string.IsNullOrWhiteSpace(item.AssignedStudentName)),
            _ => filteredKits
        };

        var filtered = filteredKits.ToArray();
        return View(new PortalKitsPageViewModel(portal.CustomerName, normalizedQuery, normalizedStatus, hasFault,
            deliveryFormMissing, normalizedAssignmentState,
            1, Math.Max(10, filtered.Length), filtered.Length, allKits.Length, filtered.ToArray()));
    }

    [HttpGet]
    public async Task<IActionResult> Returns(string? query, string state = "all", int page = 1,
        int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        if (portal is null) return Forbid();

        var normalizedQuery = query?.Trim() ?? string.Empty;
        var normalizedState = state is "all" or "pending" or "processing" or "returned" ? state : "all";
        var normalizedPageSize = pageSize is 10 or 25 or 50 ? pageSize : 10;
        var today = KitRental.SharedKernel.TurkeyTime.Today();
        var faultLookup = portal.Faults
            .GroupBy(item => item.ProductUnitId)
            .ToDictionary(group => group.Key, group => group.Count(item => item.Status is not (7 or 8)));
        var returnLookup = portal.Returns
            .SelectMany(request => request.Items.Select(item => new
            {
                item.AssignmentId,
                RequestId = request.Id,
                request.Status,
                request.CreatedAt
            }))
            .GroupBy(item => item.AssignmentId)
            .ToDictionary(group => group.Key,
                group => group.OrderByDescending(item => item.CreatedAt).First());

        var allReturns = portal.Kits
            .Where(item =>
                (item.AssignmentStatus == 2 && item.EndDate < today && !returnLookup.ContainsKey(item.AssignmentId)) ||
                returnLookup.ContainsKey(item.AssignmentId))
            .Select(item =>
            {
                returnLookup.TryGetValue(item.AssignmentId, out var currentReturn);
                var returnState = currentReturn is null
                    ? "pending"
                    : currentReturn.Status switch
                    {
                        1 => "processing",
                        2 => "processing",
                        3 => "returned",
                        _ => "pending"
                    };
                var stateLabel = returnState switch
                {
                    "processing" => "İade Sürecinde",
                    "returned" => "İade Edildi",
                    _ => "İade Bekleniyor"
                };
                return new PortalReturnListItemViewModel(
                    item.ProductUnitId,
                    item.AssignmentId,
                    currentReturn?.RequestId,
                    item.KitName,
                    item.KitSku,
                    item.SerialNumber,
                    item.OrderNumber,
                    item.StartDate,
                    item.EndDate,
                    (int)item.UnitStatus,
                    (int)item.AssignmentStatus,
                    currentReturn is null ? 0 : currentReturn.Status,
                    faultLookup.TryGetValue(item.ProductUnitId, out var openFaultCount) ? openFaultCount : 0,
                    returnState,
                    stateLabel,
                    item.StudentOrderLocked);
            })
            .Where(item => normalizedState == "all" || item.ReturnStateKey == normalizedState)
            .Where(item => normalizedQuery.Length == 0 ||
                item.KitName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                item.KitSku.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                item.SerialNumber.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                item.OrderNumber.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.ReturnStatus)
            .ThenBy(item => item.EndDate)
            .ThenBy(item => item.KitName)
            .ToArray();

        var firstItem = allReturns.Length == 0 ? 0 : 1;
        var lastItem = allReturns.Length;

        return View(new PortalReturnsPageViewModel(portal.CustomerName, normalizedQuery, normalizedState,
            1, Math.Max(10, allReturns.Length), allReturns.Length, portal.Kits.Count, 1, firstItem, lastItem,
            allReturns));
    }

    [HttpGet]
    public async Task<IActionResult> FindKit(string? identifier, CancellationToken cancellationToken)
    {
        var value = QrCodeValue.Normalize(identifier);
        if (value.Length == 0)
            return View(new PortalKitLookupPageViewModel(string.Empty, false, null));
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        if (portal is null) return Forbid();
        var kit = portal.Kits.FirstOrDefault(item => item.AssignmentStatus == 2 &&
            (string.Equals(item.SerialNumber, value, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(item.QrCode, value, StringComparison.OrdinalIgnoreCase)));
        if (kit is null)
            return View(new PortalKitLookupPageViewModel(value, true,
                "Bu kodla eşleşen, hesabınıza ait aktif bir kiralık kit bulunamadı."));
        return RedirectToAction(nameof(Kit), new { id = kit.ProductUnitId });
    }

    [HttpGet]
    public async Task<IActionResult> Kit(Guid id, CancellationToken cancellationToken)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        if (portal is null) return Forbid();
        var kit = portal.Kits.FirstOrDefault(item => item.ProductUnitId == id);
        return kit is null ? NotFound() : View(new PortalKitDetailPageViewModel(kit,
            portal.Faults.Where(fault => fault.ProductUnitId == id).ToArray()));
    }

    [HttpGet]
    public IActionResult Qr(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200) return BadRequest();
        return File(PngByteQRCodeHelper.GetQRCode(value, QRCodeGenerator.ECCLevel.Q, 8), "image/png");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmDelivery(Guid id, CancellationToken cancellationToken)
    {
        var result = await apiClient.ConfirmPortalOrderDeliveryAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Teslimat onaylandı. Kitleriniz artık kullanımınızda görünüyor."
            : result.Error ?? "Teslimat onaylanamadı.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> NewFault(Guid? assignmentId, CancellationToken cancellationToken)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        if (portal is null) return Forbid();
        var activeKits = portal.Kits
            .Where(item => item.AssignmentStatus == 2 && !item.IsReturned)
            .ToArray();
        var selectedAssignmentId = assignmentId.HasValue && activeKits.Any(item => item.AssignmentId == assignmentId)
            ? assignmentId.Value
            : activeKits.FirstOrDefault()?.AssignmentId ?? Guid.Empty;
        return View(new PortalFaultRequestPageViewModel(BuildPortalFaultForm(portal, selectedAssignmentId), activeKits));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NewFault(PortalFaultRequestViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await apiClient.CreatePortalFaultAsync(model, cancellationToken);
            if (result.IsSuccess)
            {
                TempData["Success"] = "Arıza kaydınız oluşturuldu. Servis sürecini bu ekrandan takip edebilirsiniz.";
                return RedirectToAction(nameof(Faults), new { state = "open" });
            }
            ModelState.AddModelError(string.Empty, result.Error ?? "Arıza kaydı oluşturulamadı.");
        }
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        if (portal is null) return Forbid();
        var activeKits = portal.Kits
            .Where(item => item.AssignmentStatus == 2 && !item.IsReturned)
            .ToArray();
        var selectedKit = activeKits.FirstOrDefault(item => item.AssignmentId == model.AssignmentId);
        model.KitName = selectedKit?.KitName ?? model.KitName;
        model.SerialNumber = selectedKit?.SerialNumber ?? model.SerialNumber;
        return View(new PortalFaultRequestPageViewModel(model, activeKits));
    }

    private static PortalFaultRequestViewModel BuildPortalFaultForm(CustomerPortalViewModel portal, Guid assignmentId)
    {
        var kit = portal.Kits.FirstOrDefault(item => item.AssignmentId == assignmentId);
        var student = portal.RentalCohorts
            .SelectMany(cohort => cohort.Students)
            .FirstOrDefault(item => item.AssignmentId == assignmentId);
        var address = portal.Addresses.FirstOrDefault();
        return new PortalFaultRequestViewModel
        {
            AssignmentId = assignmentId,
            KitName = kit?.KitName ?? string.Empty,
            SerialNumber = kit?.SerialNumber ?? string.Empty,
            ReporterName = student?.FullName
                ?? kit?.AssignedStudentName
                ?? address?.ContactName
                ?? string.Empty,
            ReporterPhone = student?.GuardianPhone
                ?? kit?.AssignedStudentGuardianPhone
                ?? address?.Phone
                ?? string.Empty,
            ReporterAddress = student?.DeliveryAddress
                ?? student?.AddressLine
                ?? kit?.AssignedStudentAddressLine
                ?? address?.Line1
                ?? string.Empty
        };
    }

    public async Task<IActionResult> Fault(Guid id, CancellationToken cancellationToken)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        var fault = portal?.Faults.SingleOrDefault(item => item.Id == id);
        return fault is null ? NotFound() : View(fault);
    }
}
