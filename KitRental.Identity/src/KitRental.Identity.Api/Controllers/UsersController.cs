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
    public async Task<IActionResult> GetUsers(int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var users = (await service.GetUsersAsync(cancellationToken)).ToArray();
        var validPageSize = Math.Clamp(pageSize ?? 20, 1, 5000);
        var totalPages = Math.Max(1, (int)Math.Ceiling(users.Length / (double)validPageSize));
        var validPage = Math.Clamp(page ?? 1, 1, totalPages);

        return Ok(new
        {
            Page = validPage,
            PageSize = validPageSize,
            TotalCount = users.Length,
            TotalPages = totalPages,
            Items = users.Skip((validPage - 1) * validPageSize).Take(validPageSize).ToArray()
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateUserAsync(
            new CreateUserCommand(request.Email, request.DisplayName, request.Password, request.Role, request.CustomerId),
            cancellationToken);
        return Created($"/api/users/{result.Id}", result);
    }
}
