namespace KitRental.Core.Domain.Support;

public sealed class PublicFormAccessToken
{
    private PublicFormAccessToken(Guid id, Guid productUnitId, string tokenHash, DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        ProductUnitId = productUnitId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid ProductUnitId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }

    public static PublicFormAccessToken Create(Guid id, Guid productUnitId, string tokenHash,
        DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        if (productUnitId == Guid.Empty) throw new ArgumentException("Kit kimliği zorunludur.", nameof(productUnitId));
        if (string.IsNullOrWhiteSpace(tokenHash)) throw new ArgumentException("Token özeti zorunludur.", nameof(tokenHash));
        if (expiresAt <= createdAt) throw new ArgumentException("Token bitiş zamanı başlangıçtan sonra olmalıdır.", nameof(expiresAt));

        return new PublicFormAccessToken(id, productUnitId, tokenHash.Trim(), createdAt, expiresAt);
    }

    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;

    public void MarkUsed(DateTimeOffset usedAt)
    {
        LastUsedAt = usedAt;
    }
}
