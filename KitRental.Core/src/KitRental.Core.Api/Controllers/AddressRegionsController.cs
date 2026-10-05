using KitRental.Core.Application.Kargonomi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Core.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/address-regions")]
public sealed class AddressRegionsController(IKargonomiClient client) : ControllerBase
{
    [HttpGet("cities")]
    public async Task<IActionResult> Cities(CancellationToken cancellationToken) =>
        Ok(await client.GetStatesAsync(cancellationToken));

    [HttpGet("districts/{cityId:int:min(1)}")]
    public async Task<IActionResult> Districts(int cityId, CancellationToken cancellationToken) =>
        Ok(await client.GetCitiesAsync(cityId, cancellationToken));
}
