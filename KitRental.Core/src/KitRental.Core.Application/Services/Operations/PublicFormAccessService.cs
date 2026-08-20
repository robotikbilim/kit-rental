using KitRental.Core.Application.Abstractions;
using KitRental.Core.Application.Common;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Support;
using KitRental.SharedKernel;
using System.Security.Cryptography;
using System.Text;

namespace KitRental.Core.Application.Operations;

public sealed record PublicFormAccessTokenResponse(string Token, DateTimeOffset ExpiresAt);

public sealed class PublicFormAccessService(ICoreRepository repository, TimeProvider timeProvider)
{
    public async Task<PublicFormAccessTokenResponse> CreateAsync(string qrCode, CancellationToken cancellationToken)
    {
        var unit = await FindUnitByQrCodeAsync(qrCode, cancellationToken);
        var now = timeProvider.GetTurkeyNow();
        var rawToken = CreateToken();
        var token = PublicFormAccessToken.Create(Guid.NewGuid(), unit.Id, Hash(rawToken), now, now.AddDays(1));

        await repository.AddPublicFormAccessTokenAsync(token, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return new PublicFormAccessTokenResponse(rawToken, token.ExpiresAt);
    }

    public async Task<ProductUnit> ResolveProductUnitAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ResourceNotFoundException("Public form bağlantısı geçersiz veya süresi dolmuş.");

        var accessToken = await repository.GetPublicFormAccessTokenByHashAsync(Hash(token), cancellationToken)
            ?? throw new ResourceNotFoundException("Public form bağlantısı geçersiz veya süresi dolmuş.");
        var now = timeProvider.GetTurkeyNow();
        if (accessToken.IsExpired(now))
            throw new ResourceNotFoundException("Public form bağlantısının süresi dolmuş. Lütfen QR kodu yeniden okutun.");

        var unit = await repository.GetProductUnitAsync(accessToken.ProductUnitId, cancellationToken)
            ?? throw new ResourceNotFoundException("Bu public form bağlantısıyla eşleşen fiziksel kit bulunamadı.");
        accessToken.MarkUsed(now);
        await repository.SaveChangesAsync(cancellationToken);
        return unit;
    }

    private async Task<ProductUnit> FindUnitByQrCodeAsync(string qrCode, CancellationToken cancellationToken) =>
        (await repository.GetProductUnitsAsync(cancellationToken))
            .SingleOrDefault(item => string.Equals(item.QrCode, qrCode.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ResourceNotFoundException("Bu QR kodla eşleşen fiziksel kit bulunamadı.");

    private static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()));
        return Convert.ToHexString(bytes);
    }

    private static string CreateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
