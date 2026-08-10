using System.Net;
using System.Net.Mail;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using KitRental.Core.Application.Abstractions;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Support;
using KitRental.Core.Domain.Notifications;

namespace KitRental.Core.Api;

public interface IEmailNotificationService
{
    Task NotifyAdminsOfFaultAsync(FaultTicket ticket, string eventDescription, CancellationToken cancellationToken);
    Task NotifyAdminsOfRentalRequestAsync(RentalOrder order, CancellationToken cancellationToken);
}

public sealed class EmailNotificationService(
    ICoreRepository repository,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<EmailNotificationService> logger) : IEmailNotificationService
{
    private readonly HtmlEncoder _html = HtmlEncoder.Default;

    public async Task NotifyAdminsOfFaultAsync(FaultTicket ticket, string eventDescription,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled()) return;
        var recipients = await GetAdminRecipientsAsync(cancellationToken);
        if (recipients.Count == 0) return;
        var customer = await repository.GetCustomerAsync(ticket.CustomerId, cancellationToken);
        var kit = await GetKitLabelAsync(ticket.ProductUnitId, cancellationToken);
        await TrySendAsync(recipients, $"Arıza kaydı: {ticket.Number} · {kit}",
            $"""
             <h2>{E(eventDescription)}</h2>
             <p><strong>Kayıt:</strong> {E(ticket.Number)}<br>
             <strong>Müşteri:</strong> {E(customer?.Name ?? "-")}<br>
             <strong>Kit:</strong> {E(kit)}</p>
             <p><strong>Arıza nedeni:</strong><br>{E(ticket.Description)}</p>
             <p>İşleme almak için yönetim panelindeki <strong>Arızalar</strong> ekranını açın.</p>
             """, cancellationToken);
    }

    public async Task NotifyAdminsOfRentalRequestAsync(RentalOrder order, CancellationToken cancellationToken)
    {
        if (!IsEnabled()) return;
        var recipients = await GetAdminRecipientsAsync(cancellationToken);
        if (recipients.Count == 0) return;
        var customer = await repository.GetCustomerAsync(order.CustomerId, cancellationToken);
        var models = (await repository.GetProductModelsAsync(cancellationToken)).ToDictionary(model => model.Id);
        var lines = string.Join("", order.Lines.Select(line =>
            $"<li>{E(models.TryGetValue(line.ProductModelId, out var model) ? model.Name : "Eğitim kiti")} × {line.Quantity}</li>"));
        await TrySendAsync(recipients, $"Yeni kiralama talebi: {order.OrderNumber}",
            $"""
             <h2>Yeni kiralama talebi oluşturuldu</h2>
             <p><strong>Talep:</strong> {E(order.OrderNumber)}<br>
             <strong>Müşteri:</strong> {E(customer?.Name ?? "-")}<br>
             <strong>Dönem:</strong> {order.Period?.StartDate:dd.MM.yyyy} – {order.Period?.EndDate:dd.MM.yyyy}</p>
             <ul>{lines}</ul>
             <p>Talebi değerlendirmek için yönetim panelindeki <strong>Siparişler</strong> ekranını açın.</p>
             """, cancellationToken);
    }

    private bool IsEnabled() =>
        configuration.GetValue<bool>("Email:Enabled") &&
        !string.IsNullOrWhiteSpace(configuration["Email:Smtp:Host"]);

    private async Task<string> GetKitLabelAsync(Guid productUnitId, CancellationToken cancellationToken)
    {
        var unit = await repository.GetProductUnitAsync(productUnitId, cancellationToken);
        if (unit is null) return "Fiziksel kit";
        var model = await repository.GetProductModelAsync(unit.ProductModelId, cancellationToken);
        return $"{model?.Name ?? "Eğitim kiti"} · {unit.SerialNumber}";
    }

    private async Task<IReadOnlyCollection<EmailRecipient>> GetAdminRecipientsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("identity-notifications");
            using var request = new HttpRequestMessage(HttpMethod.Get,
                "/api/internal/notification-recipients/admins");
            request.Headers.TryAddWithoutValidation("X-Internal-Api-Key",
                configuration["Notifications:InternalApiKey"]);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Admin e-posta alıcıları Identity servisinden alınamadı. HTTP {StatusCode}.",
                    response.StatusCode);
                return [];
            }
            return await response.Content.ReadFromJsonAsync<EmailRecipient[]>(cancellationToken) ?? [];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
        {
            logger.LogWarning(exception, "Admin e-posta alıcıları Identity servisinden alınamadı.");
            return [];
        }
    }

    private async Task TrySendAsync(IReadOnlyCollection<EmailRecipient> recipients, string subject,
        string body, CancellationToken cancellationToken)
    {
        foreach (var recipient in recipients.Where(item => !string.IsNullOrWhiteSpace(item.Email))
                     .DistinctBy(item => item.Email, StringComparer.OrdinalIgnoreCase))
        {
            var renderedBody = WrapBody(recipient.DisplayName, body);
            try
            {
                using var message = new MailMessage
                {
                    From = new MailAddress(configuration["Email:FromAddress"]!, configuration["Email:FromName"]),
                    Subject = subject,
                    Body = renderedBody,
                    IsBodyHtml = true,
                    BodyEncoding = Encoding.UTF8,
                    SubjectEncoding = Encoding.UTF8
                };
                message.To.Add(recipient.Email);
                using var smtp = CreateSmtpClient();
                await smtp.SendMailAsync(message, cancellationToken);
                await RecordDeliveryAsync(recipient, subject, renderedBody, EmailDeliveryStatus.Sent, null,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is SmtpException or InvalidOperationException or FormatException)
            {
                await RecordDeliveryAsync(recipient, subject, renderedBody, EmailDeliveryStatus.Failed,
                    exception.Message, cancellationToken);
                logger.LogError(exception, "E-posta bildirimi {Recipient} alıcısına gönderilemedi.", recipient.Email);
            }
        }
    }

    private async Task RecordDeliveryAsync(EmailRecipient recipient, string subject, string body,
        EmailDeliveryStatus status, string? error, CancellationToken cancellationToken)
    {
        await repository.AddEmailDeliveryAsync(EmailDelivery.Create(recipient.Email, recipient.DisplayName,
            subject, body, status, DateTimeOffset.UtcNow, error), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private SmtpClient CreateSmtpClient()
    {
        var smtp = new SmtpClient(configuration["Email:Smtp:Host"],
            configuration.GetValue("Email:Smtp:Port", 587))
        {
            EnableSsl = configuration.GetValue("Email:Smtp:EnableSsl", true),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };
        var username = configuration["Email:Smtp:Username"];
        if (!string.IsNullOrWhiteSpace(username))
            smtp.Credentials = new NetworkCredential(username, configuration["Email:Smtp:Password"]);
        return smtp;
    }

    private string WrapBody(string displayName, string content) =>
        $"""
         <!doctype html><html lang="tr"><body style="margin:0;background:#f3f6f4;font-family:Arial,sans-serif;color:#17231e">
         <div style="max-width:640px;margin:24px auto;padding:28px;background:#fff;border:1px solid #dfe7e1;border-radius:16px">
         <p>Merhaba {E(displayName)},</p>{content}
         <p style="margin-top:28px;color:#6a756f;font-size:12px">Bu e-posta KitRental tarafından otomatik gönderildi.</p>
         </div></body></html>
         """;

    private string E(string value) => _html.Encode(value);
}

public sealed record EmailRecipient(string Email, string DisplayName);
