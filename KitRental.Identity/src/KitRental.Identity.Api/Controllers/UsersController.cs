using KitRental.Identity.Api.Contracts.Requests;
using KitRental.Identity.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Identity.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "SystemAdmin,OperationsManager")]
public sealed class UsersController(IdentityService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await service.GetUsersAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateUserAsync(
            new CreateUserCommand(request.Email, request.DisplayName, request.Password, request.Role, request.CustomerId),
            cancellationToken);
        return Created($"/api/users/{result.Id}", result);
    }
}
