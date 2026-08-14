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
    public async Task<IActionResult> Orders(CancellationToken cancellationToken)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        return portal is null ? Forbid() : View(portal);
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
                item.OrderNumber.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase));
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
                    stateLabel);
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
        var kit = portal.Kits.FirstOrDefault(item => item.ProductUnitId == id && item.AssignmentStatus == 2);
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
        var activeKits = portal.Kits.Where(item => item.AssignmentStatus == 2).ToArray();
        return View(new PortalFaultRequestPageViewModel(new PortalFaultRequestViewModel
        {
            AssignmentId = assignmentId.HasValue && activeKits.Any(item => item.AssignmentId == assignmentId)
                ? assignmentId.Value : activeKits.FirstOrDefault()?.AssignmentId ?? Guid.Empty
        }, activeKits));
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
        return portal is null ? Forbid() : View(new PortalFaultRequestPageViewModel(model,
            portal.Kits.Where(item => item.AssignmentStatus == 2).ToArray()));
    }

    public async Task<IActionResult> Fault(Guid id, CancellationToken cancellationToken)
    {
        var portal = await apiClient.GetCustomerPortalAsync(cancellationToken);
        var fault = portal?.Faults.SingleOrDefault(item => item.Id == id);
        return fault is null ? NotFound() : View(fault);
    }
}
