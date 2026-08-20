using KitRental.Core.Api.Contracts.Requests;
using KitRental.Core.Application.CustomerPortal;
using KitRental.Core.Application.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Core.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class PublicController : CoreApiControllerBase
{
    [AllowAnonymous]
    [HttpPost("public/form-access/{qrCode}")]
    public async Task<IActionResult> Post_publicFormAccessQrCode(string qrCode,
        [FromServices] PublicFormAccessService accessService, CancellationToken cancellationToken)
    {
        return Ok(await accessService.CreateAsync(qrCode, cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("public/faults/kit/{token}")]
    public async Task<IActionResult> Get_publicFaultsKitQrCode_0(string token,
        [FromServices] PublicFormAccessService accessService, [FromServices] OperationsService service,
        CancellationToken cancellationToken)
    {
        var unit = await accessService.ResolveProductUnitAsync(token, cancellationToken);
        return Ok(await service.GetPublicFaultKitAsync(unit.QrCode, cancellationToken));
    }

    [Authorize]
    [HttpGet("public/faults/kit-by-qr/{qrCode}")]
    public async Task<IActionResult> Get_publicFaultsKitByQrCode(string qrCode,
        [FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetPublicFaultKitAsync(qrCode, cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("public/deliveries/context/{token}")]
    public async Task<IActionResult> Get_publicDeliveriesContextQrCode_1(string token,
        [FromServices] PublicFormAccessService accessService, [FromServices] OperationsService service,
        CancellationToken cancellationToken)
    {
        var unit = await accessService.ResolveProductUnitAsync(token, cancellationToken);
        return Ok(await service.GetPublicKitDeliveryContextAsync(unit.QrCode, cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("public/faults/context/{token}")]
    public async Task<IActionResult> Get_publicFaultsContextQrCode_2(string token,
        [FromServices] PublicFormAccessService accessService, [FromServices] OperationsService service,
        CancellationToken cancellationToken)
    {
        var unit = await accessService.ResolveProductUnitAsync(token, cancellationToken);
        return Ok(await service.GetPublicFaultContextAsync(unit.QrCode, cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("public/returns/context/{token}")]
    public async Task<IActionResult> Get_publicReturnsContextQrCode(string token,
        [FromServices] PublicFormAccessService accessService, [FromServices] CustomerPortalService service,
        CancellationToken cancellationToken)
    {
        var unit = await accessService.ResolveProductUnitAsync(token, cancellationToken);
        return Ok(await service.GetPublicKitReturnContextAsync(unit.QrCode, cancellationToken));
    }

    [AllowAnonymous]
    [HttpPost("public/faults")]
    public async Task<IActionResult> Post_publicFaults_3(PublicFaultRequest request,
        [FromServices] PublicFormAccessService accessService, [FromServices] OperationsService service,
        [FromServices] IEmailNotificationService notifications, CancellationToken cancellationToken)
    {
        var unit = await accessService.ResolveProductUnitAsync(request.Token, cancellationToken);
        var result = request.FaultId.HasValue
                ? await service.UpdatePublicFaultAsync(request.FaultId.Value, unit.QrCode, request.ReporterName,
                    request.ReporterPhone, request.ReporterAddress, request.District, request.City,
                    request.Description, request.Latitude, request.Longitude, cancellationToken)
                : await service.OpenPublicFaultAsync(new OpenPublicFaultCommand(
                    unit.QrCode, request.ReporterName, request.ReporterPhone, request.ReporterAddress,
                    request.District, request.City, request.Description, request.Latitude, request.Longitude),
                    cancellationToken);
        await notifications.NotifyAdminsOfFaultAsync(result, "QR üzerinden yeni arıza kaydı oluşturuldu",
            cancellationToken);
        return Created($"/api/public/faults/{result.Id}", new { result.Id, result.Number });
    }

    [AllowAnonymous]
    [HttpPost("public/returns")]
    public async Task<IActionResult> Post_publicReturns_4(PublicKitReturnRequest request,
        [FromServices] PublicFormAccessService accessService, [FromServices] CustomerPortalService service,
        CancellationToken cancellationToken)
    {
        var unit = await accessService.ResolveProductUnitAsync(request.Token, cancellationToken);
        var result = await service.CreatePublicKitReturnAsync(new CreatePublicKitReturnCommand(
                unit.QrCode, request.RequesterName, request.RequesterPhone, request.District,
                request.City, request.ReturnAddress, request.Latitude, request.Longitude, request.ReturnReason,
                request.DeliveryMethod), cancellationToken);
        return Created($"/api/public/returns/{result.Id}", result);
    }

    [AllowAnonymous]
    [HttpPost("public/deliveries")]
    public async Task<IActionResult> Post_publicDeliveries_5(PublicKitDeliveryRequest request,
        [FromServices] PublicFormAccessService accessService, [FromServices] OperationsService service,
        CancellationToken cancellationToken)
    {
        var unit = await accessService.ResolveProductUnitAsync(request.Token, cancellationToken);
        var result = await service.CreatePublicKitDeliveryAsync(new CreatePublicKitDeliveryCommand(
                unit.QrCode, request.RecipientName, request.RecipientPhone,
                request.AddressLine, request.District, request.City, request.Latitude, request.Longitude), cancellationToken);
        return Created($"/api/public/deliveries/{result.Id}", result);
    }

    [AllowAnonymous]
    [HttpGet("public/fault-guides")]
    public async Task<IActionResult> Get_publicFaultGuides_6([FromServices] OperationsService service, CancellationToken cancellationToken)
    {
        return Ok(await service.GetFaultGuideEntriesAsync(true, cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("public/fault-guides/{token}")]
    public async Task<IActionResult> Get_publicFaultGuidesQrCode_7(string token,
        [FromServices] PublicFormAccessService accessService, [FromServices] OperationsService service,
        CancellationToken cancellationToken)
    {
        var unit = await accessService.ResolveProductUnitAsync(token, cancellationToken);
        return Ok(await service.GetPublicFaultGuideEntriesAsync(unit.QrCode, cancellationToken));
    }

}



