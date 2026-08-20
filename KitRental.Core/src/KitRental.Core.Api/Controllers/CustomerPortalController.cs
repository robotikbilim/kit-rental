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
    public async Task<IActionResult> Get_CustomerPortal_8([FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetOverviewAsync(GetRequiredCustomerId(), cancellationToken));
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPost("customer-portal/faults")]
    public async Task<IActionResult> Post_CustomerPortalFaults_9(PortalFaultRequest request, [FromServices] CustomerPortalService service, [FromServices] IEmailNotificationService notifications, CancellationToken cancellationToken)
    {
        var result = await service.OpenFaultAsync(new OpenPortalFaultCommand(GetRequiredCustomerId(),
                request.AssignmentId, request.ReporterName, request.ReporterPhone, request.ReporterAddress,
                request.District, request.City, request.Description, User.GetRequiredUserId()),
                cancellationToken);
        await notifications.NotifyAdminsOfFaultAsync(result, "Müşteri yeni bir arıza kaydı oluşturdu",
            cancellationToken);
        return Created($"/api/faults/{result.Id}", result);
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPost("customer-portal/orders/{orderId:guid}/confirm-delivery")]
    public async Task<IActionResult> Post_CustomerPortalOrdersOrderIdGuidConfirmDelivery_10(Guid orderId, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        return Ok(await service.ConfirmOrderDeliveryAsync(new ConfirmPortalOrderDeliveryCommand(
                GetRequiredCustomerId(), orderId, User.GetRequiredUserId()), cancellationToken));
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpGet("customer-portal/rental-periods")]
    public async Task<IActionResult> Get_CustomerPortalRentalPeriods_11([FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetRentalCohortsAsync(GetRequiredCustomerId(), cancellationToken));
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPost("customer-portal/rental-periods")]
    public async Task<IActionResult> Post_CustomerPortalRentalPeriods_12(RentalCohortRequest request, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        var result = await service.SaveRentalCohortAsync(new SaveRentalCohortCommand(null,
                GetRequiredCustomerId(), request.Name, request.StartDate, request.EndDate,
                User.GetRequiredUserId(), GetActorDisplayName()), cancellationToken);
        return Created($"/api/customer-portal/rental-periods/{result.Id}", result);
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPut("customer-portal/rental-periods/{periodId:guid}")]
    public async Task<IActionResult> Put_CustomerPortalRentalPeriodsPeriodIdGuid_13(Guid periodId, RentalCohortRequest request, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        return Ok(await service.SaveRentalCohortAsync(new SaveRentalCohortCommand(periodId,
                GetRequiredCustomerId(), request.Name, request.StartDate, request.EndDate,
                User.GetRequiredUserId(), GetActorDisplayName()), cancellationToken));
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpDelete("customer-portal/rental-periods/{periodId:guid}")]
    public async Task<IActionResult> Delete_CustomerPortalRentalPeriodsPeriodIdGuid_14(Guid periodId, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        await service.DeleteRentalCohortAsync(new DeleteRentalCohortCommand(
                    GetRequiredCustomerId(), periodId, User.GetRequiredUserId()), cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPost("customer-portal/rental-periods/{periodId:guid}/students")]
    public async Task<IActionResult> Post_CustomerPortalRentalPeriodsPeriodIdGuidStudents_15(Guid periodId, RentalCohortStudentRequest request, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        var result = await service.SaveRentalCohortStudentAsync(new SaveRentalCohortStudentCommand(null,
                GetRequiredCustomerId(), periodId, request.FullName, request.GuardianPhone, request.AddressLine,
                request.CityId, request.DistrictId, request.City, request.District,
                request.ProductModelId, User.GetRequiredUserId(), GetActorDisplayName()), cancellationToken);
        return Created($"/api/customer-portal/rental-periods/{periodId}/students/{result.Id}", result);
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPut("customer-portal/rental-periods/{periodId:guid}/students/{studentId:guid}")]
    public async Task<IActionResult> Put_CustomerPortalRentalPeriodsPeriodIdGuidStudentsStudentIdGuid_16(Guid periodId, Guid studentId, RentalCohortStudentRequest request, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        return Ok(await service.SaveRentalCohortStudentAsync(new SaveRentalCohortStudentCommand(studentId,
                GetRequiredCustomerId(), periodId, request.FullName, request.GuardianPhone, request.AddressLine,
                request.CityId, request.DistrictId, request.City, request.District,
                request.ProductModelId, User.GetRequiredUserId(), GetActorDisplayName()), cancellationToken));
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpDelete("customer-portal/rental-periods/{periodId:guid}/students/{studentId:guid}")]
    public async Task<IActionResult> Delete_CustomerPortalRentalPeriodsPeriodIdGuidStudentsStudentIdGuid_17(Guid periodId, Guid studentId, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        await service.RemoveRentalCohortStudentAsync(GetRequiredCustomerId(), periodId, studentId,
                    User.GetRequiredUserId(), GetActorDisplayName(), cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPost("customer-portal/rental-periods/{periodId:guid}/students/import")]
    public async Task<IActionResult> Post_CustomerPortalRentalPeriodsPeriodIdGuidStudentsImport_18(Guid periodId, RentalCohortStudentImportRequest request, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        return Ok(await service.ImportRentalCohortStudentsAsync(GetRequiredCustomerId(), periodId,
                request.Rows.Select(row => new ImportRentalCohortStudentCommand(row.FullName, row.GuardianPhone,
                    row.AddressLine, row.CityId, row.DistrictId, row.City, row.District, row.ProductModel)).ToArray(), User.GetRequiredUserId(), GetActorDisplayName(),
                cancellationToken));
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPost("customer-portal/rental-periods/{periodId:guid}/students/{studentId:guid}/return")]
    public async Task<IActionResult> Post_CustomerPortalRentalPeriodsPeriodIdGuidStudentsStudentIdGuidReturn_19(Guid periodId, Guid studentId, PortalStudentReturnRequest request, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        var result = await service.CreatePortalStudentReturnAsync(new CreatePortalStudentReturnCommand(
                    GetRequiredCustomerId(), periodId, studentId, User.GetRequiredUserId(), GetActorDisplayName(),
                    request.RequesterName, request.RequesterPhone, request.District, request.City,
                    request.ReturnAddress, request.ReturnReason),
                    cancellationToken);
        return Created($"/api/kit-returns/{result.Id}", result);
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPost("customer-portal/rental-periods/{periodId:guid}/order")]
    public async Task<IActionResult> Post_CustomerPortalRentalPeriodsPeriodIdGuidOrder_20(Guid periodId, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateRentalCohortOrderAsync(new CreatePortalRentalCohortOrderCommand(
                    GetRequiredCustomerId(), periodId, User.GetRequiredUserId(), GetActorDisplayName()),
                    cancellationToken);
        return Created($"/api/orders/{result.Id}", result);
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPost("customer-portal/returns")]
    public async Task<IActionResult> Post_CustomerPortalReturns_21(PortalReturnRequest request, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        var result = await service.CreatePortalReturnAsync(new CreatePortalReturnCommand(
                GetRequiredCustomerId(), request.AssignmentIds, User.GetRequiredUserId(), GetActorDisplayName()),
                cancellationToken);
        return Created($"/api/kit-returns/{result.Id}", result);
    }

    [Authorize(Roles = "CustomerAccountManager,CustomerUser")]
    [HttpPost("customer-portal/returns/{returnId:guid}/ship")]
    public async Task<IActionResult> Post_CustomerPortalReturnsReturnIdGuidShip_22(Guid returnId, PortalReturnShipmentRequest request, [FromServices] CustomerPortalService service, CancellationToken cancellationToken)
    {
        return Ok(await service.ShipPortalReturnAsync(new ShipPortalReturnCommand(GetRequiredCustomerId(),
                returnId, request.Carrier, request.TrackingNumber, User.GetRequiredUserId(), GetActorDisplayName()),
                cancellationToken));
    }

}



