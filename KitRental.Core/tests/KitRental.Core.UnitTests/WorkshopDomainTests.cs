using KitRental.Core.Domain.Manufacturing;
using KitRental.Core.Domain.Warehouse;
using KitRental.SharedKernel;

namespace KitRental.Core.UnitTests;

public sealed class WorkshopDomainTests
{
    [Fact]
    public void ComponentStockDoesNotAllowNegativeBalance()
    {
        var stock = ComponentStock.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        stock.Apply(5);

        var exception = Assert.Throws<DomainException>(() => stock.Apply(-6));

        Assert.Equal("component_stock.insufficient", exception.Code);
        Assert.Equal(5, stock.Quantity);
    }

    [Fact]
    public void BillOfMaterialsRejectsDuplicateComponentLines()
    {
        var componentId = Guid.NewGuid();

        var exception = Assert.Throws<DomainException>(() => BillOfMaterials.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            [(componentId, 2), (componentId, 1)]));

        Assert.Equal("bom.component_duplicate", exception.Code);
    }

    [Fact]
    public void StockMovementDeterminesSignedQuantityFromMovementType()
    {
        var receipt = CreateMovement(StockMovementType.Receipt, 10);
        var consumption = CreateMovement(StockMovementType.Consumption, 4);

        Assert.Equal(10, receipt.SignedQuantity);
        Assert.Equal(-4, consumption.SignedQuantity);
    }

    private static StockMovement CreateMovement(StockMovementType type, decimal quantity) =>
        StockMovement.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), type, quantity, "Test", Guid.NewGuid(), DateTimeOffset.UtcNow);
}
