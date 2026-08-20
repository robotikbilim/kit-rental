using KitRental.Core.Application.Abstractions;
using KitRental.Core.Domain.Notifications;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Support;
using KitRental.SharedKernel;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Channels;

namespace KitRental.Core.Api;

public sealed class QueuedEmailNotificationService(
    IEmailNotificationQueue queue,
    ILogger<QueuedEmailNotificationService> logger) : IEmailNotificationService
{
    public Task NotifyAdminsOfFaultAsync(FaultTicket ticket, string eventDescription,
        CancellationToken cancellationToken)
    {
        if (!queue.TryEnqueue(EmailNotificationWorkItem.Fault(ticket.Id, eventDescription)))
            logger.LogWarning("Arıza e-posta bildirimi kuyruğa alınamadı. FaultTicketId={FaultTicketId}", ticket.Id);
        return Task.CompletedTask;
    }

    public Task NotifyAdminsOfRentalRequestAsync(RentalOrder order, CancellationToken cancellationToken)
    {
        if (!queue.TryEnqueue(EmailNotificationWorkItem.RentalRequest(order.Id)))
            logger.LogWarning("Kiralama talebi e-posta bildirimi kuyruğa alınamadı. OrderId={OrderId}", order.Id);
        return Task.CompletedTask;
    }
}

public interface IEmailNotificationQueue
{
    bool TryEnqueue(EmailNotificationWorkItem item);
    IAsyncEnumerable<EmailNotificationWorkItem> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed class EmailNotificationQueue : IEmailNotificationQueue
{
    private readonly Channel<EmailNotificationWorkItem> _channel = Channel.CreateUnbounded<EmailNotificationWorkItem>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public bool TryEnqueue(EmailNotificationWorkItem item) => _channel.Writer.TryWrite(item);

    public IAsyncEnumerable<EmailNotificationWorkItem> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}

public sealed class EmailNotificationWorker(
    IEmailNotificationQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<EmailNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<EmailNotificationDispatcher>();
                await dispatcher.DispatchAsync(item, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "E-posta bildirimi arka plan kuyruğunda işlenemedi. {@EmailNotificationWorkItem}", item);
            }
        }
    }
}

public sealed record EmailNotificationWorkItem(
    EmailNotificationWorkItemKind Kind,
    Guid EntityId,
    string? EventDescription = null)
{
    public static EmailNotificationWorkItem Fault(Guid faultTicketId, string eventDescription) =>
        new(EmailNotificationWorkItemKind.Fault, faultTicketId, eventDescription);

    public static EmailNotificationWorkItem RentalRequest(Guid orderId) =>
        new(EmailNotificationWorkItemKind.RentalRequest, orderId);
}

public enum EmailNotificationWorkItemKind
{
    Fault = 1,
    RentalRequest = 2
}

public sealed class EmailNotificationDispatcher(
    ICoreRepository repository,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<EmailNotificationDispatcher> logger)
{
    private readonly HtmlEncoder _html = HtmlEncoder.Default;

    public async Task DispatchAsync(EmailNotificationWorkItem item, CancellationToken cancellationToken)
    {
        switch (item.Kind)
        {
            case EmailNotificationWorkItemKind.Fault:
                await NotifyAdminsOfFaultAsync(item.EntityId, item.EventDescription ?? "Yeni arıza kaydı oluşturuldu",
                    cancellationToken);
                break;
            case EmailNotificationWorkItemKind.RentalRequest:
                await NotifyAdminsOfRentalRequestAsync(item.EntityId, cancellationToken);
                break;
        }
    }

    private async Task NotifyAdminsOfFaultAsync(Guid faultTicketId, string eventDescription,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled()) return;
        var ticket = await repository.GetFaultTicketAsync(faultTicketId, cancellationToken);
        if (ticket is null)
        {
            logger.LogWarning("E-posta bildirimi için arıza kaydı bulunamadı. FaultTicketId={FaultTicketId}", faultTicketId);
            return;
        }
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

    private async Task NotifyAdminsOfRentalRequestAsync(Guid orderId, CancellationToken cancellationToken)
    {
        if (!IsEnabled()) return;
        var order = await repository.GetOrderAsync(orderId, cancellationToken);
        if (order is null)
        {
            logger.LogWarning("E-posta bildirimi için kiralama talebi bulunamadı. OrderId={OrderId}", orderId);
            return;
        }
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
            subject, body, status, TurkeyTime.Now(), error), cancellationToken);
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
