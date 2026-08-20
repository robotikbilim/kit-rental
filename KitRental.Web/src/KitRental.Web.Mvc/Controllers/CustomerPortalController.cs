using ClosedXML.Excel;
using KitRental.SharedKernel;
using KitRental.Web.Mvc.Models;
using KitRental.Web.Mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;
using System.Globalization;
using System.Text.Json;

namespace KitRental.Web.Mvc.Controllers;

[Authorize(Roles = "CustomerAccountManager,CustomerUser")]
public sealed class CustomerPortalController(KitRentalApiClient apiClient, IWebHostEnvironment environment) : Controller
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

        const int pageSize = 20;
        var filteredList = filtered
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.Name)
            .ToArray();
        var totalCount = filteredList.Length;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var currentPage = Math.Clamp(page, 1, totalPages);
        var pageItems = filteredList
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return new RentalCohortsPageViewModel(portal.CustomerName, pageItems, form, periodNameOptions,
            normalizedPeriodName, normalizedApprovalStatus, currentPage, pageSize, totalCount);
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
        Guid? productModelId, string? assignmentState, int page = 1, CancellationToken cancellationToken = default)
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
                CityId = edit.CityId ?? 0,
                DistrictId = edit.DistrictId ?? 0,
                City = edit.City,
                District = edit.District,
                ProductModelId = edit.ProductModelId
            };
        var normalizedQuery = string.IsNullOrWhiteSpace(studentQuery) ? null : studentQuery.Trim();
        var normalizedAssignmentState = NormalizeStudentAssignmentState(assignmentState);
        var filtered = cohort.Students.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            filtered = filtered.Where(student =>
                student.FullName.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase) ||
                student.GuardianPhone.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase) ||
                student.AddressLine.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase) ||
                student.ProductModelName.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase) ||
                student.ProductModelSku.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase) ||
                (student.SerialNumber?.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                (student.QrCode?.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase) ?? false));
        }
        if (productModelId.HasValue)
        {
            filtered = filtered.Where(student => student.ProductModelId == productModelId.Value);
        }
        filtered = normalizedAssignmentState switch
        {
            "assigned" => filtered.Where(student => student.ProductUnitId.HasValue),
            "unassigned" => filtered.Where(student => !student.ProductUnitId.HasValue),
            "returning" => filtered.Where(student => student.HasActiveReturn),
            "delivered" => filtered.Where(student => student.HasDeliveryForm),
            _ => filtered
        };

        const int pageSize = 20;
        var filteredStudents = filtered
            .OrderBy(student => student.FullName)
            .ThenBy(student => student.GuardianPhone)
            .ToArray();
        var totalCount = filteredStudents.Length;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var currentPage = Math.Clamp(page, 1, totalPages);
        var pageStudents = filteredStudents
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return View(new RentalCohortDetailPageViewModel(cohort, form, portal.ProductModels, pageStudents,
            normalizedQuery, productModelId, normalizedAssignmentState, currentPage, pageSize, totalCount));
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
                TempData["Success"] = "Öğrenci kaydedildi.";
                return RedirectToAction(nameof(RentalPeriod), new { id = model.CohortId });
            }
            ModelState.AddModelError(string.Empty, result.Error ?? "Öğrenci kaydedilemedi.");
        }
        return await RentalPeriod(model.CohortId, model.Id, null, null, null, cancellationToken: cancellationToken);
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
            var address = row.Cell(3).GetString().Trim();
            var city = row.Cell(4).GetString().Trim();
            var district = row.Cell(5).GetString().Trim();
            var (cityId, districtId) = ResolveLocationIds(city, district, environment.WebRootPath);
            if (string.IsNullOrWhiteSpace(fullName) && string.IsNullOrWhiteSpace(phone) &&
                string.IsNullOrWhiteSpace(address) && string.IsNullOrWhiteSpace(city) &&
                string.IsNullOrWhiteSpace(district))
                continue;
            rows.Add(new RentalCohortStudentImportPreviewRowViewModel
            {
                FullName = fullName,
                GuardianPhone = phone,
                AddressLine = address,
                City = city,
                District = district,
                CityId = cityId,
                DistrictId = districtId,
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
                !string.IsNullOrWhiteSpace(row.AddressLine) ||
                !string.IsNullOrWhiteSpace(row.City) ||
                !string.IsNullOrWhiteSpace(row.District) ||
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
            if (string.IsNullOrWhiteSpace(row.AddressLine))
                ModelState.AddModelError($"Rows[{index}].AddressLine", "Adres bilgileri zorunludur.");
            if (string.IsNullOrWhiteSpace(row.City))
                ModelState.AddModelError($"Rows[{index}].City", "İl zorunludur.");
            if (string.IsNullOrWhiteSpace(row.District))
                ModelState.AddModelError($"Rows[{index}].District", "İlçe zorunludur.");
            if (row.CityId <= 0)
                ModelState.AddModelError($"Rows[{index}].CityId", "İl tanınamadı.");
            if (row.DistrictId <= 0)
                ModelState.AddModelError($"Rows[{index}].DistrictId", "İlçe tanınamadı.");
            if (!modelIds.Contains(row.ProductModelId))
                ModelState.AddModelError($"Rows[{index}].ProductModelId", "Her satır için eğitim kiti seçilmelidir.");
        }
        if (!ModelState.IsValid)
            return View("RentalPeriodImportPreview", model);
        var rows = model.Rows.Select(row => new
        {
            fullName = row.FullName,
            guardianPhone = TurkishPhoneNumber.Normalize(row.GuardianPhone, "Veli telefon numarası"),
            addressLine = row.AddressLine,
            cityId = row.CityId,
            districtId = row.DistrictId,
            city = row.City,
            district = row.District,
            productModel = row.ProductModelId.ToString()
        }).ToArray();
        var result = await apiClient.ImportRentalCohortStudentsAsync(model.CohortId, rows, cancellationToken);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"{rows.Length} öğrenci içe aktarıldı."
            : result.Error ?? "Öğrenci listesi içe aktarılamadı.";
        return RedirectToAction(nameof(RentalPeriod), new { id = model.CohortId });
    }

    private static (int CityId, int DistrictId) ResolveLocationIds(string cityName, string districtName, string webRootPath)
    {
        var path = Path.Combine(webRootPath, "js", "tr-city-districts.js");
        var source = System.IO.File.ReadAllText(path);
        var start = source.IndexOf('[', StringComparison.Ordinal);
        var end = source.LastIndexOf(']');
        if (start < 0 || end <= start) return (0, 0);
        using var document = JsonDocument.Parse(source[start..(end + 1)]);
        var comparer = StringComparer.Create(new CultureInfo("tr-TR"), true);
        foreach (var city in document.RootElement.EnumerateArray())
        {
            if (!comparer.Equals(city.GetProperty("name").GetString(), cityName)) continue;
            var cityId = int.Parse(city.GetProperty("code").GetString()!, CultureInfo.InvariantCulture);
            var index = 0;
            foreach (var district in city.GetProperty("districts").EnumerateArray())
            {
                index++;
                if (comparer.Equals(district.GetString(), districtName)) return (cityId, cityId * 1000 + index);
            }
            return (cityId, 0);
        }
        return (0, 0);
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
        sheet.Cell(1, 3).Value = "Adres Bilgileri";
        sheet.Cell(1, 4).Value = "İl";
        sheet.Cell(1, 5).Value = "İlçe";
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Columns(1, 5).AdjustToContents();
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
            City = string.IsNullOrWhiteSpace(student.DeliveryCity) ? student.City : student.DeliveryCity,
            District = string.IsNullOrWhiteSpace(student.DeliveryDistrict) ? student.District : student.DeliveryDistrict,
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
        var normalizedPageSize = pageSize is 10 or 25 or 50 ? pageSize : 10;
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
        var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Length / (double)normalizedPageSize));
        var normalizedPage = Math.Clamp(page, 1, totalPages);
        var pagedFaults = filtered
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToArray();

        return View(new PortalFaultsPageViewModel(portal.CustomerName, normalizedQuery, normalizedStatus,
            normalizedState, normalizedPage, normalizedPageSize, filtered.Length, allFaults.Length, pagedFaults));
    }

    [HttpGet]
    public async Task<IActionResult> Kits(string? query, int? status, bool? hasFault, bool? deliveryFormMissing,
        int page = 1,
        int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        if (portal is null) return Forbid();

        var normalizedQuery = query?.Trim() ?? string.Empty;
        var normalizedStatus = status is >= 1 and <= 8 ? status : null;
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

        var filtered = filteredKits.ToArray();
        var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Length / (double)normalizedPageSize));
        var normalizedPage = Math.Clamp(page, 1, totalPages);
        var pagedKits = filtered
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToArray();

        return View(new PortalKitsPageViewModel(portal.CustomerName, normalizedQuery, normalizedStatus, hasFault,
            deliveryFormMissing,
            normalizedPage, normalizedPageSize, filtered.Length, allKits.Length, pagedKits));
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

        var totalPages = Math.Max(1, (int)Math.Ceiling(allReturns.Length / (double)normalizedPageSize));
        var normalizedPage = Math.Clamp(page, 1, totalPages);
        var pagedReturns = allReturns
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToArray();
        var firstItem = allReturns.Length == 0 ? 0 : (normalizedPage - 1) * normalizedPageSize + 1;
        var lastItem = allReturns.Length == 0 ? 0 : Math.Min(normalizedPage * normalizedPageSize, allReturns.Length);

        return View(new PortalReturnsPageViewModel(portal.CustomerName, normalizedQuery, normalizedState,
            normalizedPage, normalizedPageSize, allReturns.Length, portal.Kits.Count, totalPages, firstItem, lastItem,
            pagedReturns));
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
            City = student?.DeliveryCity
                ?? student?.City
                ?? address?.City
                ?? string.Empty,
            District = student?.DeliveryDistrict
                ?? student?.District
                ?? address?.District
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
