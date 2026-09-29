using KitRental.Core.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitRental.Core.Api.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class IyzicoPwiController(IyzicoPwiService payments, IConfiguration configuration) : ControllerBase
{
    [HttpPost("/api/public/kit-ownership/{token}/payments")]
    public async Task<ActionResult<OwnershipPaymentStart>> Start(string token, OwnershipPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        return Ok(await payments.StartAsync(token, request, ip, cancellationToken));
    }

    [HttpGet("/api/public/kit-ownership/payments/{attemptId:guid}")]
    public async Task<ActionResult<OwnershipPaymentStatus>> Status(Guid attemptId, CancellationToken cancellationToken) =>
        await payments.GetStatusAsync(attemptId, cancellationToken) is { } status ? Ok(status) : NotFound();

    [HttpPost("/api/payments/iyzico/pwi/callback")]
    public async Task<IActionResult> Callback([FromQuery] Guid attemptId, [FromForm] string? token,
        CancellationToken cancellationToken)
    {
        token ??= Request.Query["token"].ToString();
        if (string.IsNullOrWhiteSpace(token)) return BadRequest();
        _ = await payments.VerifyTokenAsync(attemptId, token, cancellationToken);
        var uiBase = configuration["Iyzico:PublicUiBaseUrl"] ?? throw new InvalidOperationException("Iyzico:PublicUiBaseUrl ayarlanmamış.");
        return Redirect($"{uiBase.TrimEnd('/')}/ariza/sahiplenme-sonuc/{attemptId:D}");
    }

    [HttpPost("/api/payments/iyzico/pwi/webhook")]
    public async Task<IActionResult> Webhook([FromForm] IyzicoWebhookPayload payload,
        CancellationToken cancellationToken)
    {
        var allowedEvents = new[] { "PWI_TKN_FUND", "PWI_TKN_AUTH", "PWI_TKN_THREEDS_AUTH" };
        if (!allowedEvents.Contains(payload.IyziEventType, StringComparer.Ordinal)) return Ok();
        if (!payments.ValidateWebhook(Request.Headers, payload.IyziEventType, payload.IyziPaymentId,
                payload.Token, payload.PaymentConversationId, payload.Status)) return Unauthorized();
        await payments.VerifyWebhookTokenAsync(payload.Token, payload.IyziEventType,
            payload.IyziPaymentId, payload.PaymentConversationId, payload.Status, cancellationToken);
        return Ok();
    }
}

public sealed class IyzicoWebhookPayload
{
    public string Token { get; set; } = "";
    public string Status { get; set; } = "";
    public string IyziPaymentId { get; set; } = "";
    public string PaymentConversationId { get; set; } = "";
    public string IyziEventType { get; set; } = "";
}
