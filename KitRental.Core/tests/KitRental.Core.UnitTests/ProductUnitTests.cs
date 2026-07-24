using KitRental.Core.Domain.Inventory;
using KitRental.SharedKernel;

namespace KitRental.Core.UnitTests;

public sealed class ProductUnitTests
{
    [Fact]
    public void SerialNumber_ContainsNormalizedSetSkuAndCreationYear()
    {
        var serial = ProductUnitSerialNumber.Create(
            "robotik başlangıç / v2",
            new DateTimeOffset(2026, 7, 24, 10, 0, 0, TimeSpan.Zero),
            Guid.Parse("7a3f21c8-04d9-b612-aabb-ccddeeff0011"));

        Assert.Equal("ROBOTIK-BASLANGIC-V2-2026-7A3F21C8-04D9B612", serial);
    }

    [Fact]
    public void SerialNumber_UsesUniqueIdForDistinctPhysicalKits()
    {
        var createdAt = new DateTimeOffset(2026, 7, 24, 10, 0, 0, TimeSpan.Zero);

        var first = ProductUnitSerialNumber.Create("RB-SET", createdAt,
            Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var second = ProductUnitSerialNumber.Create("RB-SET", createdAt,
            Guid.Parse("22222222-2222-2222-2222-222222222222"));

        Assert.NotEqual(first, second);
        Assert.StartsWith("RB-SET-2026-", first);
        Assert.StartsWith("RB-SET-2026-", second);
    }

    private static readonly Guid ActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RentalLifecycle_RecordsEveryStatusTransition()
    {
        var unit = CreateUnit();

        unit.Reserve(ActorId, Now.AddMinutes(1));
        unit.StartPreparation(ActorId, Now.AddMinutes(2));
        unit.Dispatch(ActorId, Now.AddMinutes(3));
        unit.ConfirmDelivery(ActorId, Now.AddMinutes(4));
        unit.StartReturn(ActorId, Now.AddMinutes(5));
        unit.ReceiveForInspection(ActorId, Now.AddMinutes(6));
        unit.CompleteInspection(ProductUnitStatus.Available, ActorId, Now.AddMinutes(7), "Kontroller tamamlandı.");

        Assert.Equal(ProductUnitStatus.Available, unit.Status);
        Assert.Equal(8, unit.History.Count);
        Assert.Equal(ProductUnitStatus.UnderInspection, unit.History.Last().PreviousStatus);
        Assert.Equal(ProductUnitStatus.Available, unit.History.Last().NewStatus);
    }

    [Fact]
    public void Dispatch_RejectsInvalidStatusTransition()
    {
        var unit = CreateUnit();

        var exception = Assert.Throws<DomainException>(() => unit.Dispatch(ActorId, Now));

        Assert.Equal("product_unit.invalid_status_transition", exception.Code);
    }

    [Fact]
    public void CompleteInspection_OnlyAllowsInspectionOutcomes()
    {
        var unit = CreateUnit();
        unit.Reserve(ActorId, Now);
        unit.StartPreparation(ActorId, Now);
        unit.Dispatch(ActorId, Now);
        unit.ConfirmDelivery(ActorId, Now);
        unit.StartReturn(ActorId, Now);
        unit.ReceiveForInspection(ActorId, Now);

        var exception = Assert.Throws<DomainException>(() =>
            unit.CompleteInspection(ProductUnitStatus.WithCustomer, ActorId, Now, "Geçersiz sonuç"));

        Assert.Equal("product_unit.invalid_inspection_outcome", exception.Code);
    }

    private static ProductUnit CreateUnit() => ProductUnit.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "SN-0001",
        "QR-0001",
        ActorId,
        Now);
}
