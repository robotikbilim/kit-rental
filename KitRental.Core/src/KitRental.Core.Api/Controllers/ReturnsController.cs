using KitRental.Core.Application.CustomerPortal;
using KitRental.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Core.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class ReturnsController : CoreApiControllerBase
{
    [Authorize(Roles = "SystemAdmin,OperationsManager,WarehouseStaff")]
    [HttpPost("kit-returns/{returnId:guid}/receive")]
    public async Task<IActionResult> Post_KitReturnsReturnIdGuidReceive_23(Guid returnId, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        return Ok(await service.ReceiveKitReturnAsync(returnId, User.GetRequiredUserId(), cancellationToken));
    }

}



