using KitRental.Web.Mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Web.Mvc.Controllers;

[Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
public sealed class WorkshopController(KitRentalApiClient apiClient) : Controller
{
    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Search(string? query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return Json(Array.Empty<object>());
        return Json(await apiClient.SearchComponentsAsync(query.Trim(), cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Component(Guid id, CancellationToken cancellationToken)
    {
        var result = await apiClient.GetComponentLocatorAsync(id, cancellationToken);
        return result is null ? NotFound() : Json(result);
    }
}
