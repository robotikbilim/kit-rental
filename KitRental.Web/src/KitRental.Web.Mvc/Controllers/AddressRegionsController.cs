using KitRental.Web.Mvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Web.Mvc.Controllers;

[AllowAnonymous]
[Route("address-regions")]
public sealed class AddressRegionsController(KitRentalApiClient apiClient) : Controller
{
    [HttpGet("cities")]
    public async Task<IActionResult> Cities(CancellationToken cancellationToken)
    {
        var regions = await apiClient.GetAddressCitiesAsync(cancellationToken);
        return regions is null ? StatusCode(StatusCodes.Status503ServiceUnavailable) : Json(regions);
    }

    [HttpGet("districts")]
    public async Task<IActionResult> Districts(int cityId, CancellationToken cancellationToken)
    {
        if (cityId <= 0) return BadRequest();
        var regions = await apiClient.GetAddressDistrictsAsync(cityId, cancellationToken);
        return regions is null ? StatusCode(StatusCodes.Status503ServiceUnavailable) : Json(regions);
    }
}
