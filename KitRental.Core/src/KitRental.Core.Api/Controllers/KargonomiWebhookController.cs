using KitRental.Core.Application.Kargonomi;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KitRental.Core.Api.Controllers;

[ApiController]
[Route("api/kargonomi/webhooks")]
public sealed class KargonomiWebhookController(IConfiguration configuration, KargonomiShippingService service) : ControllerBase
{
    [HttpPost("shipment-updated")]
    public async Task<IActionResult> ShipmentUpdated(CancellationToken cancellationToken)
    {
        var expected = configuration["Kargonomi:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(expected))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Kargonomi webhook secret yapılandırılmamış." });

        Request.EnableBuffering();
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, cancellationToken);
        var payload = buffer.ToArray();
        var signature = Request.Headers["X-Webhook-Signature"].ToString();
        if (!IsValidSignature(payload, signature, expected))
            return Unauthorized();

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("shipment", out var shipment))
                return BadRequest(new { message = "shipment alanı zorunludur." });

            var shipmentId = shipment.TryGetProperty("id", out var idElement) && idElement.TryGetInt32(out var id)
                ? id
                : 0;
            if (shipmentId <= 0)
                return BadRequest(new { message = "shipment.id zorunludur." });

            await service.ApplyWebhookAsync(
                shipmentId,
                ReadString(shipment, "status"),
                ReadString(shipment, "status_label"),
                ReadString(shipment, "shipping_webservice_tracking_code"),
                ReadString(shipment, "description"),
                cancellationToken);
        }
        catch (JsonException)
        {
            return BadRequest(new { message = "Geçersiz webhook JSON gövdesi." });
        }

        return Ok(new { received = true });
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;

    private static bool IsValidSignature(byte[] payload, string signature, string secret)
    {
        if (string.IsNullOrWhiteSpace(signature))
            return false;
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload);
        var hex = Convert.ToHexString(hash);
        var base64 = Convert.ToBase64String(hash);
        return FixedTimeEquals(signature, hex) || FixedTimeEquals(signature, base64);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left.Trim());
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
