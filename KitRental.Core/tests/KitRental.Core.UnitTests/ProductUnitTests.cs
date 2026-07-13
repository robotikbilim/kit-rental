using KitRental.Core.Domain.Inventory;
using KitRental.SharedKernel;

namespace KitRental.Core.UnitTests;

public sealed class ProductUnitTests
{
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
