using KitRental.Core.Application.Abstractions;
using KitRental.Core.Application.Common;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Manufacturing;
using KitRental.Core.Domain.Warehouse;

namespace KitRental.Core.Application.Inventory;

public sealed record ProductUnitProduction(ProductModel Model, IReadOnlyCollection<Guid> ProductUnitIds);

public sealed class ProductUnitStockConsumptionPlanner(ICoreRepository repository)
{
    public async Task<IReadOnlyCollection<StockMovement>> CreateMovementsAsync(
        IReadOnlyCollection<ProductUnitProduction> production,
        Guid actorId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var plans = new List<(ProductUnitProduction Production, BillOfMaterials Bom)>();
        foreach (var item in production)
        {
            var bom = await repository.GetActiveBillOfMaterialsAsync(item.Model.Id, cancellationToken);
            if (bom is not null)
                plans.Add((item, bom));
        }
        if (plans.Count == 0)
            return [];

        var components = (await repository.GetComponentsAsync(cancellationToken)).ToDictionary(item => item.Id);
        var stocks = await repository.GetComponentStocksAsync(null, null, cancellationToken);
        var availableByLocation = stocks.ToDictionary(
            item => (item.ComponentId, item.StorageLocationId), item => item.Quantity);
        var requirements = plans.SelectMany(plan => plan.Bom.Lines.Select(line => new
            {
                line.ComponentId,
                Quantity = line.Quantity * plan.Production.ProductUnitIds.Count
            }))
            .GroupBy(item => item.ComponentId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        foreach (var (componentId, required) in requirements)
        {
            if (!components.TryGetValue(componentId, out var component))
                throw new ResourceNotFoundException("Reçetedeki komponentlerden biri bulunamadı.");
            var available = stocks.Where(item => item.ComponentId == componentId).Sum(item => item.Quantity);
            if (available < required)
                throw new ConflictException("physical_kit.component_stock_insufficient",
                    $"{component.Name} stoğu yetersiz. Gereken: {required:0.###} {component.UnitOfMeasure}, mevcut: {available:0.###} {component.UnitOfMeasure}.");
        }

        var movements = new List<StockMovement>();
        foreach (var (item, bom) in plans)
        {
            var fullReference = $"Fiziksel kit üretimi: {item.Model.Sku}/v{bom.Version}";
            var reference = fullReference.Length <= 500 ? fullReference : fullReference[..500];
            foreach (var productUnitId in item.ProductUnitIds)
            {
                foreach (var line in bom.Lines)
                {
                    var component = components[line.ComponentId];
                    var locations = availableByLocation.Where(entry =>
                            entry.Key.ComponentId == line.ComponentId && entry.Value > 0)
                        .OrderByDescending(entry => entry.Key.StorageLocationId == component.DefaultStorageLocationId)
                        .ThenBy(entry => entry.Key.StorageLocationId)
                        .ToArray();
                    var remaining = line.Quantity;
                    foreach (var location in locations)
                    {
                        var quantity = Math.Min(location.Value, remaining);
                        if (quantity <= 0)
                            continue;
                        movements.Add(StockMovement.Create(Guid.NewGuid(), line.ComponentId,
                            location.Key.StorageLocationId, StockMovementType.Consumption, quantity, reference,
                            actorId, occurredAt, productUnitId: productUnitId));
                        availableByLocation[location.Key] -= quantity;
                        remaining -= quantity;
                        if (remaining == 0)
                            break;
                    }
                }
            }
        }
        return movements;
    }

    public async Task<IReadOnlyCollection<StockMovement>> CreateRestorationMovementsAsync(
        ProductUnit unit,
        ProductModel model,
        Guid actorId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var movements = await repository.GetStockMovementsAsync(null, cancellationToken);
        var consumed = movements.Where(item => item.ProductUnitId == unit.Id &&
                item.Type == StockMovementType.Consumption)
            .GroupBy(item => (item.ComponentId, item.StorageLocationId))
            .Select(group => (group.Key.ComponentId, group.Key.StorageLocationId,
                Quantity: group.Sum(item => item.Quantity)))
            .ToArray();

        if (consumed.Length == 0)
            consumed = await CreateLegacyRestorationPlanAsync(unit, cancellationToken);

        var reference = $"Fiziksel kit stok iadesi: {model.Sku}/{unit.SerialNumber}";
        if (reference.Length > 500)
            reference = reference[..500];
        return consumed.Select(item => StockMovement.Create(Guid.NewGuid(), item.ComponentId,
            item.StorageLocationId, StockMovementType.AdjustmentIncrease, item.Quantity, reference, actorId,
            occurredAt, productUnitId: unit.Id)).ToArray();
    }

    private async Task<(Guid ComponentId, Guid StorageLocationId, decimal Quantity)[]> CreateLegacyRestorationPlanAsync(
        ProductUnit unit,
        CancellationToken cancellationToken)
    {
        var bom = await repository.GetActiveBillOfMaterialsAsync(unit.ProductModelId, cancellationToken);
        if (bom is null)
            return [];
        var components = (await repository.GetComponentsAsync(cancellationToken)).ToDictionary(item => item.Id);
        var stocks = await repository.GetComponentStocksAsync(null, null, cancellationToken);
        var movements = await repository.GetStockMovementsAsync(null, cancellationToken);
        var result = new List<(Guid, Guid, decimal)>();
        foreach (var line in bom.Lines)
        {
            if (!components.TryGetValue(line.ComponentId, out var component))
                throw new ResourceNotFoundException("Reçetedeki komponentlerden biri bulunamadı.");
            var locationId = component.DefaultStorageLocationId
                ?? movements.FirstOrDefault(item => item.ComponentId == line.ComponentId &&
                    item.Type == StockMovementType.Consumption)?.StorageLocationId
                ?? stocks.FirstOrDefault(item => item.ComponentId == line.ComponentId)?.StorageLocationId
                ?? throw new ConflictException("physical_kit.stock_return_location_missing",
                    $"{component.Name} komponentinin iade edileceği raf bulunamadı.");
            result.Add((line.ComponentId, locationId, line.Quantity));
        }
        return result.ToArray();
    }
}
