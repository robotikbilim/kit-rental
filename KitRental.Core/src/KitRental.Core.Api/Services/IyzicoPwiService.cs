using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Iyzipay;
using Iyzipay.Model;
using Iyzipay.Request;
using KitRental.Core.Application.Abstractions;
using KitRental.Core.Application.Operations;
using KitRental.Core.Domain.Auditing;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Payments;
using KitRental.Core.Domain.Rentals;
using KitRental.Core.Domain.Returns;
using Microsoft.AspNetCore.DataProtection;

namespace KitRental.Core.Api.Services;

public sealed record OwnershipPaymentRequest(string FirstName, string LastName, string IdentityNumber,
    string Email, string Phone, string Address, string City, string? PostalCode);
public sealed record OwnershipPaymentStart(Guid AttemptId, string PaymentPageUrl, DateTimeOffset ExpiresAt);
public sealed record OwnershipPaymentStatus(Guid AttemptId, string Status, DateTimeOffset? ExpiresAt);

public sealed class IyzicoPwiService(ICoreRepository repository, PublicFormAccessService publicForms,
    IDataProtectionProvider dataProtectionProvider, IConfiguration configuration, TimeProvider timeProvider)
{
    private static readonly Guid SystemActorId = Guid.Parse("b9d7720f-7ef9-4fe6-a397-41d240ef7774");
    private const decimal Price = 2350m;
    private const decimal PaidPrice = 2820m;
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("KitRental.Iyzico.Pwi.Token.v1");

    public async Task<OwnershipPaymentStart> StartAsync(string publicToken, OwnershipPaymentRequest input,
        string ipAddress, CancellationToken cancellationToken)
    {
        var unit = await publicForms.ResolveProductUnitAsync(publicToken, cancellationToken);
        if (unit.Status != ProductUnitStatus.WithCustomer)
            throw new InvalidOperationException("Bu kit şu anda sahiplenme ödemesine uygun değil.");
        var assignments = (await repository.GetAssignmentsForProductUnitAsync(unit.Id, cancellationToken))
            .Where(item => item.Status == RentalAssignmentStatus.Active).ToArray();
        if (assignments.Length != 1)
            throw new InvalidOperationException("Kit için tek bir aktif kiralama ataması bulunmalıdır.");
        var assignment = assignments[0];
        var returnRequests = await repository.GetKitReturnRequestsAsync(assignment.CustomerId, cancellationToken);
        if (returnRequests.Any(request => request.Items.Any(item => item.ProductUnitId == unit.Id) &&
                                         request.Status != KitReturnStatus.Received))
            throw new InvalidOperationException("İade süreci başlamış bu kit için ödeme başlatılamaz.");

        var previous = await repository.GetKitOwnershipPaymentsForProductUnitAsync(unit.Id, cancellationToken);
        foreach (var old in previous) old.MarkExpired(timeProvider.GetUtcNow());
        if (previous.Any(payment => payment.Status is KitOwnershipPaymentStatus.Initializing or
                KitOwnershipPaymentStatus.Pending or KitOwnershipPaymentStatus.AwaitingReview or
                KitOwnershipPaymentStatus.Succeeded))
            throw new InvalidOperationException("Bu kit için geçerli bir sahiplenme ödemesi zaten var.");

        var id = Guid.NewGuid();
        var conversationId = id.ToString("N");
        var basketId = $"KIT-{unit.Id:N}";
        var payment = KitOwnershipPayment.Create(id, unit.Id, assignment.Id, assignment.CustomerId,
            Price, PaidPrice, conversationId, basketId, timeProvider.GetUtcNow());
        await repository.AddKitOwnershipPaymentAsync(payment, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        var apiBase = RequiredHttpsSetting("Iyzico:PublicApiBaseUrl");
        var callbackUrl = $"{apiBase}/api/payments/iyzico/pwi/callback?attemptId={id:D}";
        var options = CreateOptions();
        var request = new CreatePayWithIyzicoInitializeRequest
        {
            Locale = Locale.TR.ToString(), ConversationId = conversationId,
            Price = Price.ToString("0.00", CultureInfo.InvariantCulture),
            PaidPrice = PaidPrice.ToString("0.00", CultureInfo.InvariantCulture),
            Currency = Currency.TRY.ToString(), BasketId = basketId,
            PaymentGroup = PaymentGroup.PRODUCT.ToString(), CallbackUrl = callbackUrl,
            Buyer = new Buyer
            {
                Id = assignment.CustomerId.ToString("N"), Name = input.FirstName.Trim(), Surname = input.LastName.Trim(),
                IdentityNumber = input.IdentityNumber.Trim(), Email = input.Email.Trim(), GsmNumber = NormalizeTurkishPhone(input.Phone),
                RegistrationAddress = input.Address.Trim(), City = input.City.Trim(), Country = "Turkey",
                ZipCode = input.PostalCode ?? "", Ip = ipAddress
            },
            BillingAddress = new Address
            {
                ContactName = $"{input.FirstName} {input.LastName}".Trim(), City = input.City.Trim(), Country = "Turkey",
                Description = input.Address.Trim(), ZipCode = input.PostalCode ?? ""
            },
            ShippingAddress = new Address
            {
                ContactName = $"{input.FirstName} {input.LastName}".Trim(), City = input.City.Trim(), Country = "Turkey",
                Description = input.Address.Trim(), ZipCode = input.PostalCode ?? ""
            },
            BasketItems = [new BasketItem
            {
                Id = unit.Id.ToString("N"), Name = $"Robotik Eğitim Kiti {unit.SerialNumber}", Category1 = "Eğitim Kiti",
                ItemType = BasketItemType.PHYSICAL.ToString(), Price = Price.ToString("0.00", CultureInfo.InvariantCulture)
            }]
        };
        PayWithIyzicoInitializeResource result;
        try
        {
            result = await PayWithIyzicoInitialize.Create(request, options);
        }
        catch
        {
            payment.MarkInitializationFailed(timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(cancellationToken);
            throw;
        }
        if (!string.Equals(result.Status, Status.SUCCESS.ToString(), StringComparison.OrdinalIgnoreCase) ||
            !ValidateInitializeSignature(result, conversationId) ||
            string.IsNullOrWhiteSpace(result.Token) || string.IsNullOrWhiteSpace(result.PayWithIyzicoPageUrl))
        {
            payment.MarkInitializationFailed(timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("iyzico ödeme sayfası oluşturulamadı.");
        }

        var expiry = result.TokenExpireTime is > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(result.TokenExpireTime.Value)
            : timeProvider.GetUtcNow().AddMinutes(30);
        payment.SetInitialized(Hash(result.Token), _protector.Protect(result.Token), result.PayWithIyzicoPageUrl, expiry);
        await repository.SaveChangesAsync(cancellationToken);
        return new OwnershipPaymentStart(id, result.PayWithIyzicoPageUrl, expiry);
    }

    public async Task<OwnershipPaymentStatus?> GetStatusAsync(Guid attemptId, CancellationToken cancellationToken)
    {
        var payment = await repository.GetKitOwnershipPaymentAsync(attemptId, cancellationToken);
        if (payment is not null && payment.Status == KitOwnershipPaymentStatus.Pending)
        {
            payment.MarkExpired(timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(cancellationToken);
        }
        return payment is null ? null : new OwnershipPaymentStatus(payment.Id, payment.Status.ToString(), payment.ExpiresAt);
    }

    public async Task<OwnershipPaymentStatus?> VerifyTokenAsync(Guid attemptId, string token,
        CancellationToken cancellationToken, string? expectedPaymentId = null, string? expectedConversationId = null)
    {
        var payment = await repository.GetKitOwnershipPaymentAsync(attemptId, cancellationToken);
        if (payment is null || payment.IyzicoTokenHash != Hash(token)) return null;
        var request = new RetrievePayWithIyzicoRequest { ConversationId = payment.ConversationId, Token = token };
        var result = await PayWithIyzico.Retrieve(request, CreateOptions());
        if (!string.Equals(result.Status, Status.SUCCESS.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("iyzico ödeme sonucu sorgulanamadı.");
        if (!ValidateRetrieveSignature(result, token) ||
            !string.Equals(result.ConversationId, payment.ConversationId, StringComparison.Ordinal) ||
            (expectedPaymentId is not null && !string.Equals(result.PaymentId, expectedPaymentId, StringComparison.Ordinal)) ||
            (expectedConversationId is not null && !string.Equals(result.ConversationId, expectedConversationId, StringComparison.Ordinal)) ||
            !string.Equals(result.BasketId, payment.BasketId, StringComparison.Ordinal) ||
            !decimal.TryParse(result.Price, CultureInfo.InvariantCulture, out var price) || price != payment.Price ||
            !decimal.TryParse(result.PaidPrice, CultureInfo.InvariantCulture, out var paidPrice) || paidPrice != payment.PaidPrice ||
            !string.Equals(result.Currency, Currency.TRY.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("iyzico ödeme yanıtı beklenen siparişle eşleşmiyor.");

        var wasNewSuccess = payment.ApplyProviderResult(result.PaymentStatus ?? "", result.FraudStatus,
            result.PaymentId, result.PaymentItems?.FirstOrDefault()?.PaymentTransactionId, timeProvider.GetUtcNow());
        if (wasNewSuccess)
        {
            var unit = await repository.GetProductUnitAsync(payment.ProductUnitId, cancellationToken)
                ?? throw new InvalidOperationException("Ödemeye bağlı kit bulunamadı.");
            var assignment = await repository.GetRentalAssignmentAsync(payment.AssignmentId, cancellationToken)
                ?? throw new InvalidOperationException("Ödemeye bağlı kiralama ataması bulunamadı.");
            if (unit.Status != ProductUnitStatus.WithCustomer || assignment.Status != RentalAssignmentStatus.Active)
                throw new InvalidOperationException("Kitin aktif kiralama durumu ödeme tamamlanırken değişmiş.");
            var now = timeProvider.GetUtcNow();
            unit.ConvertRentalToSale(SystemActorId, now);
            assignment.Complete();
            var order = await repository.FindOrderByLineIdAsync(assignment.OrderLineId, cancellationToken);
            await repository.AddProductUnitActivityAsync(ProductUnitActivity.Create(Guid.NewGuid(), unit.Id,
                assignment.Id, order?.Id, null, SystemActorId, "iyzico / Kit Sahiplenme", "KitSatıldı",
                "TACEV desteğiyle kit sahiplenme ödemesi tamamlandı.", now), cancellationToken);
            await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), SystemActorId,
                "KitOwnershipPayment", payment.Id, "IyzicoPwiSucceeded", null, unit.Id.ToString(), now), cancellationToken);
        }
        await repository.SaveChangesAsync(cancellationToken);
        return new OwnershipPaymentStatus(payment.Id, payment.Status.ToString(), payment.ExpiresAt);
    }

    public async Task VerifyWebhookTokenAsync(string token, string eventType, string paymentId, string conversationId,
        string status,
        CancellationToken cancellationToken)
    {
        var payment = await repository.GetKitOwnershipPaymentByTokenHashAsync(Hash(token), cancellationToken)
            ?? throw new InvalidOperationException("iyzico bildirimi bilinen bir ödeme denemesiyle eşleşmiyor.");
        if (!string.Equals(payment.ConversationId, conversationId, StringComparison.Ordinal))
            throw new InvalidOperationException("iyzico webhook konuşma kimliği eşleşmiyor.");
        payment.RecordWebhook(eventType, paymentId, status, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        await VerifyTokenAsync(payment.Id, token, cancellationToken, paymentId, conversationId);
        payment.MarkWebhookProcessed(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
    }

    private bool ValidateRetrieveSignature(PayWithIyzico response, string token)
    {
        var values = new[] { response.PaymentStatus, response.PaymentId, response.Currency,
            response.BasketId, response.ConversationId, response.PaidPrice, response.Price, token };
        var message = string.Join(":", values.Select(value => value ?? string.Empty));
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(RequiredSecret()),
            Encoding.UTF8.GetBytes(message))).ToLowerInvariant();
        return !string.IsNullOrWhiteSpace(response.Signature) && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(response.Signature.ToLowerInvariant()));
    }

    private bool ValidateInitializeSignature(PayWithIyzicoInitializeResource response, string conversationId)
    {
        var message = $"{conversationId}:{response.Token}";
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(RequiredSecret()),
            Encoding.UTF8.GetBytes(message))).ToLowerInvariant();
        return !string.IsNullOrWhiteSpace(response.Signature) && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(response.Signature.ToLowerInvariant()));
    }

    public bool ValidateWebhook(IHeaderDictionary headers, string eventType, string paymentId, string token,
        string conversationId, string status)
    {
        if (!headers.TryGetValue("X-IYZ-SIGNATURE-V3", out var values)) return false;
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(RequiredSecret()),
            Encoding.UTF8.GetBytes(RequiredSecret() + eventType + paymentId + token + conversationId + status))).ToLowerInvariant();
        var supplied = values.ToString();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied.ToLowerInvariant()));
    }

    private Options CreateOptions() => new()
    {
        ApiKey = configuration["Iyzico:ApiKey"] ?? throw new InvalidOperationException("Iyzico:ApiKey ayarlanmamış."),
        SecretKey = RequiredSecret(),
        BaseUrl = configuration["Iyzico:BaseUrl"] ?? "https://sandbox-api.iyzipay.com"
    };

    private string RequiredSecret() => configuration["Iyzico:SecretKey"] ?? throw new InvalidOperationException("Iyzico:SecretKey ayarlanmamış.");
    private static string NormalizeTurkishPhone(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0090", StringComparison.Ordinal)) digits = digits[4..];
        else if (digits.StartsWith("90", StringComparison.Ordinal)) digits = digits[2..];
        else if (digits.StartsWith('0')) digits = digits[1..];
        return $"+90{digits}";
    }
    private string RequiredHttpsSetting(string key)
    {
        var value = configuration[key] ?? throw new InvalidOperationException($"{key} ayarlanmamış.");
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException($"{key} HTTPS adresi olmalıdır.");
        return value.TrimEnd('/');
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
