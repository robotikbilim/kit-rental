using KitRental.Core.Api.Contracts.Requests;
using KitRental.Core.Application.CustomerPortal;
using KitRental.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Core.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class CustomerPortalController : CoreApiControllerBase
{
    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpGet("customer-portal")]
    public async Task<IActionResult> GetCustomerPortal([FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetOverviewAsync(GetRequiredCustomerId(), cancellationToken));
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPost("customer-portal/faults")]
    public async Task<IActionResult> CreateCustomerPortalFault(PortalFaultRequest request, [FromServices] CustomerPortalService service, [FromServices] IEmailNotificationService notifications, CancellationToken cancellationToken)
    {
        var result = await service.OpenFaultAsync(new OpenPortalFaultCommand(GetRequiredCustomerId(),
                request.AssignmentId, request.ReporterName, request.ReporterPhone, request.ReporterAddress,
                request.Description, User.GetRequiredUserId()),
                cancellationToken);
        await notifications.NotifyAdminsOfFaultAsync(result, "Müşteri yeni bir arıza kaydı oluşturdu",
            cancellationToken);
        return Created($"/api/faults/{result.Id}", result);
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPost("customer-portal/orders/{orderId:guid}/delivery-confirmations")]
    public async Task<IActionResult> CreateCustomerPortalOrderDeliveryConfirmation(Guid orderId, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        return Ok(await service.ConfirmOrderDeliveryAsync(new ConfirmPortalOrderDeliveryCommand(
                GetRequiredCustomerId(), orderId, User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpGet("customer-portal/rental-periods")]
    public async Task<IActionResult> GetCustomerPortalRentalPeriods(int? page, int? pageSize, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        return Ok((await service.GetRentalCohortsAsync(GetRequiredCustomerId(), cancellationToken)).ToPagedResponse(page, pageSize));
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPost("customer-portal/rental-periods")]
    public async Task<IActionResult> CreateCustomerPortalRentalPeriod(RentalCohortRequest request, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        var result = await service.SaveRentalCohortAsync(new SaveRentalCohortCommand(null,
                GetRequiredCustomerId(), request.Name, request.StartDate, request.EndDate,
                User.GetRequiredUserId(), GetActorDisplayName()), cancellationToken);
        return Created($"/api/customer-portal/rental-periods/{result.Id}", result);
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPut("customer-portal/rental-periods/{periodId:guid}")]
    public async Task<IActionResult> UpdateCustomerPortalRentalPeriod(Guid periodId, RentalCohortRequest request, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        return Ok(await service.SaveRentalCohortAsync(new SaveRentalCohortCommand(periodId,
                GetRequiredCustomerId(), request.Name, request.StartDate, request.EndDate,
                User.GetRequiredUserId(), GetActorDisplayName()), cancellationToken));
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpDelete("customer-portal/rental-periods/{periodId:guid}")]
    public async Task<IActionResult> DeleteCustomerPortalRentalPeriod(Guid periodId, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        await service.DeleteRentalCohortAsync(new DeleteRentalCohortCommand(
                    GetRequiredCustomerId(), periodId, User.GetRequiredUserId()), cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPost("customer-portal/rental-periods/{periodId:guid}/students")]
    public async Task<IActionResult> CreateCustomerPortalRentalPeriodStudent(Guid periodId, RentalCohortStudentRequest request, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        var result = await service.SaveRentalCohortStudentAsync(new SaveRentalCohortStudentCommand(null,
                GetRequiredCustomerId(), periodId, request.FullName, request.GuardianPhone, request.AddressLine ?? string.Empty,
                request.ProductModelId, User.GetRequiredUserId(), GetActorDisplayName()), cancellationToken);
        return Created($"/api/customer-portal/rental-periods/{periodId}/students/{result.Id}", result);
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPut("customer-portal/rental-periods/{periodId:guid}/students/{studentId:guid}")]
    public async Task<IActionResult> UpdateCustomerPortalRentalPeriodStudent(Guid periodId, Guid studentId, RentalCohortStudentRequest request, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        return Ok(await service.SaveRentalCohortStudentAsync(new SaveRentalCohortStudentCommand(studentId,
                GetRequiredCustomerId(), periodId, request.FullName, request.GuardianPhone, request.AddressLine ?? string.Empty,
                request.ProductModelId, User.GetRequiredUserId(), GetActorDisplayName()), cancellationToken));
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpDelete("customer-portal/rental-periods/{periodId:guid}/students/{studentId:guid}")]
    public async Task<IActionResult> DeleteCustomerPortalRentalPeriodStudent(Guid periodId, Guid studentId, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        await service.RemoveRentalCohortStudentAsync(GetRequiredCustomerId(), periodId, studentId,
                    User.GetRequiredUserId(), GetActorDisplayName(), cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPost("customer-portal/rental-periods/{periodId:guid}/student-imports")]
    public async Task<IActionResult> CreateCustomerPortalRentalPeriodStudentImport(Guid periodId, RentalCohortStudentImportRequest request, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        return Ok(await service.ImportRentalCohortStudentsAsync(GetRequiredCustomerId(), periodId,
                request.Rows.Select(row => new ImportRentalCohortStudentCommand(row.FullName, row.GuardianPhone,
                    row.AddressLine ?? string.Empty, row.ProductModel)).ToArray(), User.GetRequiredUserId(), GetActorDisplayName(),
                cancellationToken));
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPost("customer-portal/rental-periods/{periodId:guid}/students/{studentId:guid}/returns")]
    public async Task<IActionResult> CreateCustomerPortalRentalPeriodStudentReturn(Guid periodId, Guid studentId, PortalStudentReturnRequest request, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        var result = await service.CreatePortalStudentReturnAsync(new CreatePortalStudentReturnCommand(
                    GetRequiredCustomerId(), periodId, studentId, User.GetRequiredUserId(), GetActorDisplayName(),
                    request.RequesterName, request.RequesterPhone, request.ReturnAddress, request.ReturnReason),
                    cancellationToken);
        return Created($"/api/kit-returns/{result.Id}", result);
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPost("customer-portal/rental-periods/{periodId:guid}/orders")]
    public async Task<IActionResult> CreateCustomerPortalRentalPeriodOrder(Guid periodId, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateRentalCohortOrderAsync(new CreatePortalRentalCohortOrderCommand(
                    GetRequiredCustomerId(), periodId, User.GetRequiredUserId(), GetActorDisplayName()),
                    cancellationToken);
        return Created($"/api/orders/{result.Id}", result);
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPost("customer-portal/returns")]
    public async Task<IActionResult> CreateCustomerPortalReturn(PortalReturnRequest request, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        var result = await service.CreatePortalReturnAsync(new CreatePortalReturnCommand(
                GetRequiredCustomerId(), request.AssignmentIds, User.GetRequiredUserId(), GetActorDisplayName()),
                cancellationToken);
        return Created($"/api/kit-returns/{result.Id}", result);
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPost("customer-portal/returns/{returnId:guid}/shipments")]
    public async Task<IActionResult> CreateCustomerPortalReturnShipment(Guid returnId, PortalReturnShipmentRequest request, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        return Ok(await service.ShipPortalReturnAsync(new ShipPortalReturnCommand(GetRequiredCustomerId(),
                returnId, request.Carrier, request.TrackingNumber, User.GetRequiredUserId(), GetActorDisplayName()),
                cancellationToken));
    }

}



