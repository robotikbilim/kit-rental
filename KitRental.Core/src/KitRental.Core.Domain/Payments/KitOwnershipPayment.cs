using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Payments;

public enum KitOwnershipPaymentStatus
{
    Initializing = 1,
    Pending = 2,
    AwaitingReview = 3,
    Succeeded = 4,
    Failed = 5,
    Expired = 6
}

public sealed class KitOwnershipPayment
{
    private KitOwnershipPayment() { }

    private KitOwnershipPayment(Guid id, Guid productUnitId, Guid assignmentId, Guid customerId,
        decimal price, decimal paidPrice, string conversationId, string basketId, DateTimeOffset createdAt)
    {
        Id = id;
        ProductUnitId = productUnitId;
        AssignmentId = assignmentId;
        CustomerId = customerId;
        Price = price;
        PaidPrice = paidPrice;
        Currency = "TRY";
        ConversationId = conversationId;
        BasketId = basketId;
        CreatedAt = createdAt;
        Status = KitOwnershipPaymentStatus.Initializing;
    }

    public Guid Id { get; private set; }
    public Guid ProductUnitId { get; private set; }
    public Guid AssignmentId { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal Price { get; private set; }
    public decimal PaidPrice { get; private set; }
    public string Currency { get; private set; } = "TRY";
    public string ConversationId { get; private set; } = string.Empty;
    public string BasketId { get; private set; } = string.Empty;
    public string? IyzicoTokenHash { get; private set; }
    public string? ProtectedIyzicoToken { get; private set; }
    public string? PaymentPageUrl { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public KitOwnershipPaymentStatus Status { get; private set; }
    public string? ProviderStatus { get; private set; }
    public int? FraudStatus { get; private set; }
    public string? PaymentId { get; private set; }
    public string? PaymentTransactionId { get; private set; }
    public string? LastWebhookEventType { get; private set; }
    public string? LastWebhookPaymentId { get; private set; }
    public string? LastWebhookStatus { get; private set; }
    public DateTimeOffset? LastWebhookReceivedAt { get; private set; }
    public DateTimeOffset? LastWebhookProcessedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public static KitOwnershipPayment Create(Guid id, Guid productUnitId, Guid assignmentId, Guid customerId,
        decimal price, decimal paidPrice, string conversationId, string basketId, DateTimeOffset createdAt)
    {
        if (new[] { id, productUnitId, assignmentId, customerId }.Any(value => value == Guid.Empty) ||
            price <= 0 || paidPrice < price || string.IsNullOrWhiteSpace(conversationId) ||
            string.IsNullOrWhiteSpace(basketId))
            throw new DomainException("kit_ownership_payment.invalid", "Kit sahiplenme ödeme bilgileri geçersiz.");

        return new KitOwnershipPayment(id, productUnitId, assignmentId, customerId, price, paidPrice,
            conversationId.Trim(), basketId.Trim(), createdAt);
    }

    public void SetInitialized(string tokenHash, string protectedToken, string paymentPageUrl,
        DateTimeOffset expiresAt)
    {
        if (Status != KitOwnershipPaymentStatus.Initializing || string.IsNullOrWhiteSpace(tokenHash) ||
            string.IsNullOrWhiteSpace(protectedToken) || !Uri.TryCreate(paymentPageUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || expiresAt <= CreatedAt)
            throw new DomainException("kit_ownership_payment.invalid_initialization", "iyzico ödeme başlatma yanıtı geçersiz.");

        IyzicoTokenHash = tokenHash;
        ProtectedIyzicoToken = protectedToken;
        PaymentPageUrl = paymentPageUrl;
        ExpiresAt = expiresAt;
        Status = KitOwnershipPaymentStatus.Pending;
    }

    public void MarkInitializationFailed(DateTimeOffset now)
    {
        if (Status != KitOwnershipPaymentStatus.Initializing)
            throw new DomainException("kit_ownership_payment.invalid_transition", "Ödeme başlatma durumu değiştirilemez.");
        Status = KitOwnershipPaymentStatus.Failed;
        ProviderStatus = "INITIALIZATION_FAILED";
        CompletedAt = now;
    }

    public bool ApplyProviderResult(string paymentStatus, int? fraudStatus, string? paymentId,
        string? paymentTransactionId, DateTimeOffset now)
    {
        if (Status == KitOwnershipPaymentStatus.Succeeded) return false;

        ProviderStatus = Limit(paymentStatus, 80);
        FraudStatus = fraudStatus;
        PaymentId = Limit(paymentId, 120);
        PaymentTransactionId = Limit(paymentTransactionId, 120);

        if (string.Equals(paymentStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase) && fraudStatus == 1)
        {
            Status = KitOwnershipPaymentStatus.Succeeded;
            CompletedAt = now;
            return true;
        }

        if (string.Equals(paymentStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase) && fraudStatus == 0)
        {
            Status = KitOwnershipPaymentStatus.AwaitingReview;
            return false;
        }

        if (string.Equals(paymentStatus, "FAILURE", StringComparison.OrdinalIgnoreCase) || fraudStatus == -1)
        {
            Status = KitOwnershipPaymentStatus.Failed;
            CompletedAt = now;
            return false;
        }

        if (Status != KitOwnershipPaymentStatus.Expired)
            Status = KitOwnershipPaymentStatus.Pending;
        return false;
    }

    public void MarkExpired(DateTimeOffset now)
    {
        if (Status != KitOwnershipPaymentStatus.Pending || !ExpiresAt.HasValue || ExpiresAt > now) return;
        Status = KitOwnershipPaymentStatus.Expired;
        CompletedAt = now;
    }

    public void RecordWebhook(string eventType, string paymentId, string status, DateTimeOffset receivedAt)
    {
        if (string.IsNullOrWhiteSpace(eventType) || string.IsNullOrWhiteSpace(paymentId) ||
            string.IsNullOrWhiteSpace(status))
            throw new DomainException("kit_ownership_payment.invalid_webhook", "iyzico webhook bilgileri geçersiz.");
        LastWebhookEventType = Limit(eventType, 80);
        LastWebhookPaymentId = Limit(paymentId, 120);
        LastWebhookStatus = Limit(status, 40);
        LastWebhookReceivedAt = receivedAt;
        LastWebhookProcessedAt = null;
    }

    public void MarkWebhookProcessed(DateTimeOffset processedAt) => LastWebhookProcessedAt = processedAt;

    private static string? Limit(string? value, int length) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, length)];
}
