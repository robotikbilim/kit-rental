using KitRental.Core.Domain.Payments;

namespace KitRental.Core.UnitTests;

public sealed class KitOwnershipPaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PwiSuccessIsFinalOnlyWhenFraudStatusIsOne()
    {
        var payment = CreatePayment();
        payment.SetInitialized("hash", "protected", "https://sandbox.iyzico.com/pay", Now.AddMinutes(30));

        Assert.False(payment.ApplyProviderResult("SUCCESS", 0, "payment-1", null, Now.AddMinutes(1)));
        Assert.Equal(KitOwnershipPaymentStatus.AwaitingReview, payment.Status);
        Assert.True(payment.ApplyProviderResult("SUCCESS", 1, "payment-1", "item-1", Now.AddMinutes(2)));
        Assert.Equal(KitOwnershipPaymentStatus.Succeeded, payment.Status);
        Assert.False(payment.ApplyProviderResult("SUCCESS", 1, "payment-1", "item-1", Now.AddMinutes(3)));
    }

    [Theory]
    [InlineData("FAILURE", 1)]
    [InlineData("SUCCESS", -1)]
    public void FailedOrRejectedProviderResultCannotSucceed(string providerStatus, int fraudStatus)
    {
        var payment = CreatePayment();
        payment.SetInitialized("hash", "protected", "https://sandbox.iyzico.com/pay", Now.AddMinutes(30));

        Assert.False(payment.ApplyProviderResult(providerStatus, fraudStatus, "payment-2", null, Now.AddMinutes(1)));
        Assert.Equal(KitOwnershipPaymentStatus.Failed, payment.Status);
    }

    private static KitOwnershipPayment CreatePayment() => KitOwnershipPayment.Create(Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), Guid.NewGuid(), 2350m, 2820m, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), Now);
}
