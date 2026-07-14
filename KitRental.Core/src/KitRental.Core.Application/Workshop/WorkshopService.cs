using KitRental.Core.Application.Abstractions;
using KitRental.Core.Application.Common;
using KitRental.Core.Domain.Auditing;
using KitRental.Core.Domain.Manufacturing;
using KitRental.Core.Domain.Warehouse;
using KitRental.Core.Domain.Inventory;
using KitRental.SharedKernel;

namespace KitRental.Core.Application.Workshop;

public sealed class WorkshopService(ICoreRepository repository, TimeProvider timeProvider)
{
    public async Task<ComponentResponse> CreateComponentAsync(CreateComponentCommand command, CancellationToken cancellationToken)
    {
        var defaultStorageLocationId = command.DefaultStorageLocationId;
        if (!defaultStorageLocationId.HasValue)
            defaultStorageLocationId = (await repository.GetStorageLocationsAsync(cancellationToken))
                .SingleOrDefault(item => item.IsDefaultForNewComponents)?.Id;
        await EnsureDefaultLocationAsync(defaultStorageLocationId, cancellationToken);
        if (command.InitialStock < 0)
            throw new DomainException("component.initial_stock_invalid", "Başlangıç stoğu negatif olamaz.");
        if (command.InitialStock > 0 && !defaultStorageLocationId.HasValue)
            throw new DomainException("component.initial_stock_location_required",
                "Başlangıç stoğu girmek için bir raf konumu seçilmelidir.");
        var component = Component.Create(Guid.NewGuid(), command.Name, command.Sku, command.UnitOfMeasure,
            command.MinimumStock, command.ImageUrl, defaultStorageLocationId);
        try
        {
            await repository.AddComponentAsync(component, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflictException("component.sku_not_unique", exception.Message);
        }

        await AuditAsync(command.ActorId, nameof(Component), component.Id, "Created", null, component.Sku, cancellationToken);
        if (command.InitialStock > 0)
        {
            var movement = StockMovement.Create(Guid.NewGuid(), component.Id,
                defaultStorageLocationId!.Value, StockMovementType.Receipt, command.InitialStock,
                "Komponent oluşturma başlangıç stoğu", command.ActorId, timeProvider.GetUtcNow());
            await repository.ApplyStockMovementsAsync([movement], cancellationToken);
        }
        else
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        return MapComponent(component, command.InitialStock);
    }

    public async Task<ComponentLocatorResponse> AdjustStockAsync(AdjustComponentStockCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Change == 0 || Math.Abs(command.Change) > 999999)
            throw new DomainException("component.stock_change_invalid", "Stok değişimi sıfırdan farklı ve geçerli bir miktar olmalıdır.");
        var component = await repository.GetComponentAsync(command.ComponentId, cancellationToken)
            ?? throw new ResourceNotFoundException("Komponent bulunamadı.");
        var stocks = (await repository.GetComponentStocksAsync(component.Id, null, cancellationToken))
            .OrderByDescending(item => item.StorageLocationId == component.DefaultStorageLocationId)
            .ThenBy(item => item.StorageLocationId)
            .ToArray();
        var now = timeProvider.GetUtcNow();
        var movements = new List<StockMovement>();

        if (command.Change > 0)
        {
            var locationId = component.DefaultStorageLocationId ?? stocks.FirstOrDefault()?.StorageLocationId
                ?? throw new ConflictException("component.stock_location_required",
                    "Stok artırmak için komponente varsayılan bir raf tanımlayın.");
            movements.Add(StockMovement.Create(Guid.NewGuid(), component.Id, locationId,
                StockMovementType.AdjustmentIncrease, command.Change, "Hızlı stok artırımı", command.ActorId, now));
        }
        else
        {
            var required = Math.Abs(command.Change);
            var available = stocks.Sum(item => item.Quantity);
            if (available < required)
                throw new ConflictException("component.stock_insufficient",
                    $"{component.Name} stoğu yetersiz. Mevcut stok: {available:0.###} {component.UnitOfMeasure}.");
            var remaining = required;
            foreach (var stock in stocks)
            {
                var quantity = Math.Min(stock.Quantity, remaining);
                if (quantity <= 0) continue;
                movements.Add(StockMovement.Create(Guid.NewGuid(), component.Id, stock.StorageLocationId,
                    StockMovementType.AdjustmentDecrease, quantity, "Hızlı stok azaltımı", command.ActorId, now));
                remaining -= quantity;
                if (remaining == 0) break;
            }
        }

        await AuditAsync(command.ActorId, nameof(ComponentStock), component.Id, "QuickAdjusted", null,
            command.Change.ToString("0.###"), cancellationToken);
        await repository.ApplyStockMovementsAsync(movements, cancellationToken);
        return await GetComponentLocatorAsync(component.Id, cancellationToken);
    }

    public async Task<ComponentResponse> UpdateComponentAsync(UpdateComponentCommand command, CancellationToken cancellationToken)
    {
        var component = await repository.GetComponentAsync(command.Id, cancellationToken)
            ?? throw new ResourceNotFoundException("Komponent bulunamadı.");
        await EnsureDefaultLocationAsync(command.DefaultStorageLocationId, cancellationToken);
        if ((await repository.GetComponentsAsync(cancellationToken)).Any(item => item.Id != component.Id &&
            item.Sku.Equals(command.Sku.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException("component.sku_not_unique", "Bu SKU başka bir komponentte kullanılıyor.");
        var previous = component.Sku;
        component.Update(command.Name, command.Sku, command.UnitOfMeasure, command.MinimumStock, command.ImageUrl,
            command.DefaultStorageLocationId);
        await AuditAsync(command.ActorId, nameof(Component), component.Id, "Updated", previous, component.Sku, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        var total = (await repository.GetComponentStocksAsync(component.Id, null, cancellationToken)).Sum(item => item.Quantity);
        return MapComponent(component, total);
    }

    public async Task DeleteComponentAsync(Guid id, Guid actorId, CancellationToken cancellationToken)
    {
        var component = await repository.GetComponentAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("Komponent bulunamadı.");
        var isInRecipe = (await repository.GetActiveBillOfMaterialsAsync(cancellationToken))
            .Any(bom => bom.Lines.Any(line => line.ComponentId == id));
        var hasStockHistory = (await repository.GetStockMovementsAsync(id, cancellationToken)).Count > 0 ||
            (await repository.GetComponentStocksAsync(id, null, cancellationToken)).Count > 0;
        if (isInRecipe || hasStockHistory)
            throw new ConflictException("component.in_use", "Reçetede veya stok hareketlerinde kullanılan komponent silinemez.");
        await repository.RemoveComponentAsync(component, cancellationToken);
        await AuditAsync(actorId, nameof(Component), id, "Deleted", component.Sku, null, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ComponentResponse>> GetComponentsAsync(bool lowStockOnly, CancellationToken cancellationToken)
    {
        var components = await repository.GetComponentsAsync(cancellationToken);
        var balances = await repository.GetComponentStocksAsync(null, null, cancellationToken);
        var totals = balances.GroupBy(item => item.ComponentId).ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
        return components
            .Select(component => MapComponent(component, totals.GetValueOrDefault(component.Id)))
            .Where(component => !lowStockOnly || component.IsLowStock)
            .OrderBy(component => component.Name)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<ComponentSearchResponse>> SearchComponentsAsync(
        string? query,
        int limit,
        CancellationToken cancellationToken)
    {
        var normalized = query?.Trim() ?? string.Empty;
        if (normalized.Length < 2)
            return [];
        var take = Math.Clamp(limit, 1, 20);
        var components = await repository.GetComponentsAsync(cancellationToken);
        var matches = components.Where(component =>
                component.Name.Contains(normalized, StringComparison.CurrentCultureIgnoreCase) ||
                component.Sku.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(component => component.Name.StartsWith(normalized, StringComparison.CurrentCultureIgnoreCase))
            .ThenBy(component => component.Name)
            .Take(take)
            .ToArray();
        var ids = matches.Select(component => component.Id).ToHashSet();
        var totals = (await repository.GetComponentStocksAsync(null, null, cancellationToken))
            .Where(stock => ids.Contains(stock.ComponentId))
            .GroupBy(stock => stock.ComponentId)
            .ToDictionary(group => group.Key, group => group.Sum(stock => stock.Quantity));
        return matches.Select(component => new ComponentSearchResponse(component.Id, component.Name, component.Sku,
            component.ImageUrl, totals.GetValueOrDefault(component.Id), component.UnitOfMeasure)).ToArray();
    }

    public async Task<ComponentLocatorResponse> GetComponentLocatorAsync(Guid componentId, CancellationToken cancellationToken)
    {
        var component = await repository.GetComponentAsync(componentId, cancellationToken)
            ?? throw new ResourceNotFoundException("Komponent bulunamadı.");
        var locations = (await repository.GetStorageLocationsAsync(cancellationToken)).ToDictionary(item => item.Id);
        var stocks = await repository.GetComponentStocksAsync(componentId, null, cancellationToken);
        var locationResponses = stocks.Where(stock => locations.ContainsKey(stock.StorageLocationId))
            .Select(stock =>
            {
                var location = locations[stock.StorageLocationId];
                return new ComponentLocationResponse(location.Id, location.Code, location.Warehouse, location.Aisle,
                    location.Rack, location.Shelf, stock.Quantity);
            })
            .OrderBy(item => item.LocationCode)
            .ToList();
        if (component.DefaultStorageLocationId.HasValue &&
            locations.TryGetValue(component.DefaultStorageLocationId.Value, out var defaultLocation) &&
            locationResponses.All(item => item.StorageLocationId != defaultLocation.Id))
            locationResponses.Add(new ComponentLocationResponse(defaultLocation.Id, defaultLocation.Code,
                defaultLocation.Warehouse, defaultLocation.Aisle, defaultLocation.Rack, defaultLocation.Shelf, 0));
        locationResponses = locationResponses.OrderBy(item => item.LocationCode).ToList();
        var total = locationResponses.Sum(item => item.Quantity);
        return new ComponentLocatorResponse(component.Id, component.Name, component.Sku, component.UnitOfMeasure,
            component.ImageUrl, total, component.MinimumStock, total <= component.MinimumStock, locationResponses);
    }

    public async Task<StorageLocationResponse> CreateLocationAsync(CreateStorageLocationCommand command, CancellationToken cancellationToken)
    {
        var location = StorageLocation.Create(Guid.NewGuid(), command.Code, command.Warehouse, command.Aisle,
            command.Rack, command.Shelf, command.IsDefaultForNewComponents);
        try
        {
            await repository.AddStorageLocationAsync(location, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflictException("storage_location.code_not_unique", exception.Message);
        }

        if (location.IsDefaultForNewComponents)
            ClearOtherDefaultLocations(location.Id, await repository.GetStorageLocationsAsync(cancellationToken));
        await AuditAsync(command.ActorId, nameof(StorageLocation), location.Id, "Created", null, location.Code, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return MapLocation(location);
    }

    public async Task<IReadOnlyCollection<StorageLocationResponse>> GetLocationsAsync(CancellationToken cancellationToken) =>
        (await repository.GetStorageLocationsAsync(cancellationToken)).Select(MapLocation).ToArray();

    public async Task<StorageLocationResponse> UpdateLocationAsync(UpdateStorageLocationCommand command,
        CancellationToken cancellationToken)
    {
        var location = await repository.GetStorageLocationAsync(command.Id, cancellationToken)
            ?? throw new ResourceNotFoundException("Raf/lokasyon bulunamadı.");
        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if ((await repository.GetStorageLocationsAsync(cancellationToken)).Any(item => item.Id != location.Id &&
            item.Code == normalizedCode))
            throw new ConflictException("storage_location.code_not_unique", "Bu raf kodu başka bir kayıtta kullanılıyor.");
        var previous = location.Code;
        location.Update(command.Code, command.Warehouse, command.Aisle, command.Rack, command.Shelf,
            command.IsDefaultForNewComponents);
        if (location.IsDefaultForNewComponents)
            ClearOtherDefaultLocations(location.Id, await repository.GetStorageLocationsAsync(cancellationToken));
        await AuditAsync(command.ActorId, nameof(StorageLocation), location.Id, "Updated", previous,
            location.Code, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return MapLocation(location);
    }

    public async Task DeleteLocationAsync(Guid id, Guid actorId, CancellationToken cancellationToken)
    {
        var location = await repository.GetStorageLocationAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("Raf/lokasyon bulunamadı.");
        if ((await repository.GetComponentStocksAsync(null, id, cancellationToken)).Count > 0 ||
            (await repository.GetStockMovementsAsync(null, cancellationToken)).Any(item => item.StorageLocationId == id))
            throw new ConflictException("storage_location.in_use", "Stok veya hareket kaydı bulunan raf silinemez.");
        foreach (var component in (await repository.GetComponentsAsync(cancellationToken))
                     .Where(item => item.DefaultStorageLocationId == id))
            component.ClearDefaultStorageLocation();
        await repository.RemoveStorageLocationAsync(location, cancellationToken);
        await AuditAsync(actorId, nameof(StorageLocation), id, "Deleted", location.Code, null, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<StockMovementResponse> ReceiveAsync(RecordStockCommand command, CancellationToken cancellationToken) =>
        await RecordAsync(command, StockMovementType.Receipt, cancellationToken);

    public async Task<StockMovementResponse> ConsumeAsync(RecordStockCommand command, CancellationToken cancellationToken) =>
        await RecordAsync(command, StockMovementType.Consumption, cancellationToken);

    public async Task<IReadOnlyCollection<StockMovementResponse>> TransferAsync(TransferStockCommand command, CancellationToken cancellationToken)
    {
        if (command.FromStorageLocationId == command.ToStorageLocationId)
            throw new ConflictException("stock_transfer.same_location", "Kaynak ve hedef lokasyon aynı olamaz.");
        await EnsureComponentAndLocationsAsync(
            command.ComponentId,
            [command.FromStorageLocationId, command.ToStorageLocationId],
            cancellationToken);

        var now = timeProvider.GetUtcNow();
        var transferId = Guid.NewGuid();
        var movements = new[]
        {
            StockMovement.Create(Guid.NewGuid(), command.ComponentId, command.FromStorageLocationId,
                StockMovementType.TransferOut, command.Quantity, command.Reference, command.ActorId, now, transferId),
            StockMovement.Create(Guid.NewGuid(), command.ComponentId, command.ToStorageLocationId,
                StockMovementType.TransferIn, command.Quantity, command.Reference, command.ActorId, now, transferId)
        };
        await AuditAsync(command.ActorId, nameof(ComponentStock), command.ComponentId, "Transferred",
            command.FromStorageLocationId.ToString(), command.ToStorageLocationId.ToString(), cancellationToken);
        await repository.ApplyStockMovementsAsync(movements, cancellationToken);
        return movements.Select(MapMovement).ToArray();
    }

    public async Task<IReadOnlyCollection<ComponentStockResponse>> GetStocksAsync(
        Guid? componentId,
        Guid? locationId,
        CancellationToken cancellationToken)
    {
        var components = (await repository.GetComponentsAsync(cancellationToken)).ToDictionary(item => item.Id);
        var locations = (await repository.GetStorageLocationsAsync(cancellationToken)).ToDictionary(item => item.Id);
        return (await repository.GetComponentStocksAsync(componentId, locationId, cancellationToken))
            .Where(item => components.ContainsKey(item.ComponentId) && locations.ContainsKey(item.StorageLocationId))
            .Select(item =>
            {
                var component = components[item.ComponentId];
                return new ComponentStockResponse(item.ComponentId, component.Name, component.Sku, item.StorageLocationId,
                    locations[item.StorageLocationId].Code, item.Quantity, component.UnitOfMeasure);
            })
            .OrderBy(item => item.ComponentName)
            .ThenBy(item => item.LocationCode)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<StockMovementResponse>> GetMovementsAsync(
        Guid? componentId,
        CancellationToken cancellationToken) =>
        (await repository.GetStockMovementsAsync(componentId, cancellationToken)).Select(MapMovement).ToArray();

    public async Task<BillOfMaterialsResponse> CreateBomAsync(CreateBillOfMaterialsCommand command, CancellationToken cancellationToken)
    {
        var product = await repository.GetProductModelAsync(command.ProductModelId, cancellationToken)
            ?? throw new ResourceNotFoundException("Reçete oluşturulacak ürün modeli bulunamadı.");
        var componentIds = command.Lines.Select(line => line.ComponentId).Distinct().ToArray();
        var components = await repository.GetComponentsAsync(cancellationToken);
        if (componentIds.Any(id => components.All(component => component.Id != id)))
            throw new ResourceNotFoundException("Reçetedeki komponentlerden biri bulunamadı.");

        var bom = BillOfMaterials.Create(Guid.NewGuid(), command.ProductModelId, command.Version,
            command.Lines.Select(line => (line.ComponentId, line.Quantity)));
        try
        {
            await repository.AddBillOfMaterialsAsync(bom, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflictException("bom.version_not_unique", exception.Message);
        }

        await AuditAsync(command.ActorId, nameof(BillOfMaterials), bom.Id, "Created", null,
            $"{product.Sku}/v{bom.Version}", cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return MapBom(bom, product, components.ToDictionary(item => item.Id));
    }

    public async Task<KitCatalogResponse> CreateKitAsync(CreateKitCommand command, CancellationToken cancellationToken)
    {
        var componentIds = command.Lines.Select(line => line.ComponentId).Distinct().ToArray();
        var components = await repository.GetComponentsAsync(cancellationToken);
        if (componentIds.Any(id => components.All(component => component.Id != id)))
            throw new ResourceNotFoundException("Reçetedeki komponentlerden biri bulunamadı.");

        var product = ProductModel.Create(Guid.NewGuid(), command.Name, command.Sku, command.Description, command.ImageUrl);
        BillOfMaterials? bom = null;
        if (command.Lines.Count > 0)
            bom = BillOfMaterials.Create(Guid.NewGuid(), product.Id, command.BomVersion,
                command.Lines.Select(line => (line.ComponentId, line.Quantity)));
        try
        {
            await repository.AddProductModelAsync(product, cancellationToken);
            if (bom is not null)
                await repository.AddBillOfMaterialsAsync(bom, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflictException("kit.catalog_conflict", exception.Message);
        }

        await AuditAsync(command.ActorId, nameof(ProductModel), product.Id, "Created", null, product.Sku, cancellationToken);
        if (bom is not null)
            await AuditAsync(command.ActorId, nameof(BillOfMaterials), bom.Id, "Created", null,
                $"{product.Sku}/v{bom.Version}", cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        if (bom is null)
            return new KitCatalogResponse(product.Id, product.Name, product.Sku, product.Description, product.ImageUrl,
                null, []);
        var mapped = MapBom(bom, product, components.ToDictionary(item => item.Id));
        return new KitCatalogResponse(product.Id, product.Name, product.Sku, product.Description, product.ImageUrl,
            mapped.Version, mapped.Lines);
    }

    public async Task<BillOfMaterialsResponse?> GetActiveBomAsync(Guid productModelId, CancellationToken cancellationToken)
    {
        var product = await repository.GetProductModelAsync(productModelId, cancellationToken)
            ?? throw new ResourceNotFoundException("Ürün modeli bulunamadı.");
        var bom = await repository.GetActiveBillOfMaterialsAsync(productModelId, cancellationToken);
        if (bom is null)
            return null;

        var components = (await repository.GetComponentsAsync(cancellationToken)).ToDictionary(item => item.Id);
        return MapBom(bom, product, components);
    }

    public async Task<IReadOnlyCollection<BuildableKitResponse>> GetBuildableKitsAsync(Guid? productModelId, CancellationToken cancellationToken)
    {
        var boms = productModelId.HasValue
            ? new[] { await repository.GetActiveBillOfMaterialsAsync(productModelId.Value, cancellationToken) }
                .Where(item => item is not null).Cast<BillOfMaterials>().ToArray()
            : (await repository.GetActiveBillOfMaterialsAsync(cancellationToken)).ToArray();
        if (productModelId.HasValue && boms.Length == 0)
            throw new ResourceNotFoundException("Ürün modeli için aktif reçete bulunamadı.");

        var components = (await repository.GetComponentsAsync(cancellationToken)).ToDictionary(item => item.Id);
        var products = new Dictionary<Guid, KitRental.Core.Domain.Inventory.ProductModel>();
        foreach (var productId in boms.Select(item => item.ProductModelId).Distinct())
        {
            var product = await repository.GetProductModelAsync(productId, cancellationToken);
            if (product is not null)
                products[productId] = product;
        }
        var stocks = await repository.GetComponentStocksAsync(null, null, cancellationToken);
        var totals = stocks.GroupBy(item => item.ComponentId).ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        return boms.Where(bom => products.ContainsKey(bom.ProductModelId)).Select(bom =>
        {
            var capacities = bom.Lines.Select(line => new
            {
                Line = line,
                Available = totals.GetValueOrDefault(line.ComponentId),
                Capacity = (int)Math.Floor(totals.GetValueOrDefault(line.ComponentId) / line.Quantity)
            }).ToArray();
            var buildable = capacities.Min(item => item.Capacity);
            var details = capacities.Select(item => new BuildableComponentResponse(
                item.Line.ComponentId,
                components[item.Line.ComponentId].Name,
                components[item.Line.ComponentId].Sku,
                components[item.Line.ComponentId].UnitOfMeasure,
                components[item.Line.ComponentId].ImageUrl,
                item.Line.Quantity,
                item.Available,
                item.Capacity,
                item.Capacity == buildable,
                item.Available <= components[item.Line.ComponentId].MinimumStock,
                Math.Max(0, item.Line.Quantity * (buildable + 1) - item.Available))).ToArray();
            var product = products[bom.ProductModelId];
            return new BuildableKitResponse(product.Id, product.Name, product.Sku, product.ImageUrl, bom.Version, buildable, details);
        }).OrderBy(item => item.ProductName).ToArray();
    }

    private async Task<StockMovementResponse> RecordAsync(
        RecordStockCommand command,
        StockMovementType type,
        CancellationToken cancellationToken)
    {
        await EnsureComponentAndLocationsAsync(command.ComponentId, [command.StorageLocationId], cancellationToken);
        var movement = StockMovement.Create(Guid.NewGuid(), command.ComponentId, command.StorageLocationId,
            type, command.Quantity, command.Reference, command.ActorId, timeProvider.GetUtcNow());
        await AuditAsync(command.ActorId, nameof(ComponentStock), command.ComponentId, type.ToString(), null,
            $"{command.Quantity}@{command.StorageLocationId}", cancellationToken);
        await repository.ApplyStockMovementsAsync([movement], cancellationToken);
        return MapMovement(movement);
    }

    private async Task EnsureComponentAndLocationsAsync(
        Guid componentId,
        IEnumerable<Guid> locationIds,
        CancellationToken cancellationToken)
    {
        if (await repository.GetComponentAsync(componentId, cancellationToken) is null)
            throw new ResourceNotFoundException("Komponent bulunamadı.");
        foreach (var locationId in locationIds)
        {
            if (await repository.GetStorageLocationAsync(locationId, cancellationToken) is null)
                throw new ResourceNotFoundException("Raf/lokasyon bulunamadı.");
        }
    }

    private async Task EnsureDefaultLocationAsync(Guid? locationId, CancellationToken cancellationToken)
    {
        if (locationId.HasValue && await repository.GetStorageLocationAsync(locationId.Value, cancellationToken) is null)
            throw new ResourceNotFoundException("Seçilen varsayılan raf bulunamadı.");
    }

    private Task AuditAsync(Guid actorId, string entityType, Guid entityId, string action,
        string? previous, string? next, CancellationToken cancellationToken) =>
        repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), actorId, entityType, entityId, action,
            previous, next, timeProvider.GetUtcNow()), cancellationToken);

    private static ComponentResponse MapComponent(Component component, decimal totalStock) =>
        new(component.Id, component.Name, component.Sku, component.UnitOfMeasure, component.MinimumStock, component.ImageUrl,
            component.DefaultStorageLocationId, totalStock, totalStock <= component.MinimumStock);

    private static StorageLocationResponse MapLocation(StorageLocation location) =>
        new(location.Id, location.Code, location.Warehouse, location.Aisle, location.Rack, location.Shelf,
            location.IsDefaultForNewComponents);

    private static void ClearOtherDefaultLocations(Guid selectedId,
        IReadOnlyCollection<StorageLocation> locations)
    {
        foreach (var location in locations.Where(item =>
                     item.Id != selectedId && item.IsDefaultForNewComponents))
            location.SetDefaultForNewComponents(false);
    }

    private static StockMovementResponse MapMovement(StockMovement movement) =>
        new(movement.Id, movement.ComponentId, movement.StorageLocationId, movement.Type, movement.Quantity,
            movement.Reference, movement.ActorId, movement.OccurredAt, movement.TransferId);

    private static BillOfMaterialsResponse MapBom(
        BillOfMaterials bom,
        KitRental.Core.Domain.Inventory.ProductModel product,
        IReadOnlyDictionary<Guid, Component> components) =>
        new(bom.Id, product.Id, product.Name, product.Sku, bom.Version,
            bom.Lines.Select(line => new BillOfMaterialsLineResponse(line.ComponentId, components[line.ComponentId].Name,
                components[line.ComponentId].Sku, line.Quantity, components[line.ComponentId].UnitOfMeasure)).ToArray());
}
