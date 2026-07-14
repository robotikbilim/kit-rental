using KitRental.Core.Application.Abstractions;
using KitRental.Core.Application.Common;
using KitRental.Core.Domain.Auditing;
using KitRental.Core.Domain.Procurement;
using KitRental.Core.Domain.Warehouse;

namespace KitRental.Core.Application.Procurement;

public sealed class SupplyNeedService(ICoreRepository repository, TimeProvider timeProvider)
{
    public async Task<IReadOnlyCollection<SupplyNeedResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var components = (await repository.GetComponentsAsync(cancellationToken)).ToDictionary(item => item.Id);
        return (await repository.GetSupplyNeedListsAsync(cancellationToken))
            .Select(item => Map(item, components)).ToArray();
    }

    public async Task<SupplyNeedResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var list = await repository.GetSupplyNeedListAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("İhtiyaç listesi bulunamadı.");
        return Map(list, (await repository.GetComponentsAsync(cancellationToken)).ToDictionary(item => item.Id));
    }

    public async Task<SupplyNeedResponse> CreateAsync(CreateSupplyNeedCommand command,
        CancellationToken cancellationToken)
    {
        var components = await EnsureComponentsAsync(command.Lines, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var list = SupplyNeedList.Create(Guid.NewGuid(), now,
            command.Lines.Select(line => (line.ComponentId, line.Quantity)));
        await repository.AddSupplyNeedListAsync(list, cancellationToken);
        await AuditAsync(command.ActorId, list.Id, "Created", null, list.Status.ToString(), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(list, components);
    }

    public async Task<SupplyNeedResponse> UpdateAsync(UpdateSupplyNeedCommand command,
        CancellationToken cancellationToken)
    {
        var list = await repository.GetSupplyNeedListAsync(command.Id, cancellationToken)
            ?? throw new ResourceNotFoundException("İhtiyaç listesi bulunamadı.");
        if (list.Status == SupplyNeedStatus.Supplied)
            throw new ConflictException("supply_need.already_supplied", "Tedarik edilmiş ihtiyaç listesi düzenlenemez.");
        var components = await EnsureComponentsAsync(command.Lines, cancellationToken);
        list.Update(command.Lines.Select(line => (line.ComponentId, line.Quantity)), timeProvider.GetUtcNow());
        await AuditAsync(command.ActorId, list.Id, "Updated", null, $"{list.Lines.Count} kalem", cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(list, components);
    }

    public async Task<SupplyNeedResponse> CompleteAsync(CompleteSupplyNeedCommand command,
        CancellationToken cancellationToken)
    {
        var list = await repository.GetSupplyNeedListAsync(command.Id, cancellationToken)
            ?? throw new ResourceNotFoundException("İhtiyaç listesi bulunamadı.");
        if (list.Status == SupplyNeedStatus.Supplied)
            throw new ConflictException("supply_need.already_supplied", "Bu ihtiyaç listesi daha önce stoğa işlendi.");
        var storageLocationId = command.StorageLocationId;
        if (storageLocationId == Guid.Empty)
        {
            var locations = await repository.GetStorageLocationsAsync(cancellationToken);
            if (locations.Count > 0)
                throw new ConflictException("supply_need.location_required", "Stokların ekleneceği depo/raf konumunu seçin.");
            var receivingLocation = StorageLocation.Create(Guid.NewGuid(), "TEDARIK-KABUL", "Ana Depo",
                "Tedarik", "Kabul", "1");
            await repository.AddStorageLocationAsync(receivingLocation, cancellationToken);
            storageLocationId = receivingLocation.Id;
        }
        else if (await repository.GetStorageLocationAsync(storageLocationId, cancellationToken) is null)
            throw new ResourceNotFoundException("Stokların ekleneceği depo/raf konumu bulunamadı.");
        var components = await EnsureComponentsAsync(command.Lines, cancellationToken);
        var now = timeProvider.GetUtcNow();
        list.Complete(command.Lines.Select(line => (line.ComponentId, line.Quantity)), now);
        var movements = list.Lines.Select(line => StockMovement.Create(Guid.NewGuid(), line.ComponentId,
            storageLocationId, StockMovementType.Receipt, line.SuppliedQuantity!.Value,
            $"İhtiyaç listesi {list.Id:N}", command.ActorId, now)).ToArray();
        await AuditAsync(command.ActorId, list.Id, "Supplied", SupplyNeedStatus.Pending.ToString(),
            $"{movements.Length} kalem stoğa eklendi", cancellationToken);
        await repository.ApplyStockMovementsAsync(movements, cancellationToken);
        return Map(list, components);
    }

    public async Task DeleteAsync(Guid id, Guid actorId, CancellationToken cancellationToken)
    {
        var list = await repository.GetSupplyNeedListAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("İhtiyaç listesi bulunamadı.");
        await repository.RemoveSupplyNeedListAsync(list, cancellationToken);
        await AuditAsync(actorId, list.Id, "Deleted", list.Status.ToString(), null, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, Component>> EnsureComponentsAsync(
        IReadOnlyCollection<SupplyNeedLineCommand> lines, CancellationToken cancellationToken)
    {
        var components = (await repository.GetComponentsAsync(cancellationToken)).ToDictionary(item => item.Id);
        if (lines.Any(line => !components.ContainsKey(line.ComponentId)))
            throw new ResourceNotFoundException("Listedeki komponentlerden biri bulunamadı.");
        return components;
    }

    private Task AuditAsync(Guid actorId, Guid entityId, string action, string? previous, string? next,
        CancellationToken cancellationToken) => repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), actorId,
        nameof(SupplyNeedList), entityId, action, previous, next, timeProvider.GetUtcNow()), cancellationToken);

    private static SupplyNeedResponse Map(SupplyNeedList list, IReadOnlyDictionary<Guid, Component> components) =>
        new(list.Id, list.Status, list.CreatedAt, list.UpdatedAt, list.Lines.Select(line =>
        {
            var component = components[line.ComponentId];
            return new SupplyNeedLineResponse(component.Id, component.Name, component.Sku,
                component.UnitOfMeasure, line.Quantity, line.SuppliedQuantity);
        }).ToArray());
}
