using KitRental.Core.Application.Abstractions;
using KitRental.Core.Application.Common;
using KitRental.Core.Domain.Auditing;
using KitRental.Core.Domain.Inventory;
using KitRental.SharedKernel;

namespace KitRental.Core.Application.Inventory;

public sealed class InventoryService(ICoreRepository repository, TimeProvider timeProvider)
{
    public async Task<ProductModelResponse> CreateModelAsync(
        CreateProductModelCommand command,
        CancellationToken cancellationToken)
    {
        var model = ProductModel.Create(Guid.NewGuid(), command.Name, command.Sku, command.Description, command.ImageUrl);

        try
        {
            await repository.AddProductModelAsync(model, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflictException("product_model.sku_not_unique", exception.Message);
        }

        await repository.AddAuditEntryAsync(
            new AuditEntry(Guid.NewGuid(), command.ActorId, nameof(ProductModel), model.Id, "Created", null, model.Sku, timeProvider.GetUtcNow()),
            cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return MapModel(model);
    }

    public async Task<IReadOnlyCollection<ProductModelResponse>> GetModelsAsync(CancellationToken cancellationToken) =>
        (await repository.GetProductModelsAsync(cancellationToken)).Select(MapModel).ToArray();

    public async Task<ProductModelResponse> GetModelAsync(Guid id, CancellationToken cancellationToken)
    {
        var model = await repository.GetProductModelAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("Eğitim kiti bulunamadı.");
        return MapModel(model);
    }

    public async Task<ProductUnitResponse> CreateUnitAsync(
        CreateProductUnitCommand command,
        CancellationToken cancellationToken)
    {
        if (await repository.GetProductModelAsync(command.ProductModelId, cancellationToken) is null)
        {
            throw new ResourceNotFoundException("Ürün modeli bulunamadı.");
        }

        var serialNumber = string.IsNullOrWhiteSpace(command.SerialNumber)
            ? GenerateSerialNumber(timeProvider.GetUtcNow())
            : command.SerialNumber;
        var qrCode = string.IsNullOrWhiteSpace(command.QrCode)
            ? $"KITRENTAL:{serialNumber}"
            : command.QrCode;
        var unit = ProductUnit.Create(
            Guid.NewGuid(),
            command.ProductModelId,
            serialNumber,
            qrCode,
            command.ActorId,
            timeProvider.GetUtcNow());

        try
        {
            await repository.AddProductUnitAsync(unit, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflictException("product_unit.identifier_not_unique", exception.Message);
        }

        await repository.AddAuditEntryAsync(
            new AuditEntry(Guid.NewGuid(), command.ActorId, nameof(ProductUnit), unit.Id, "Created", null, unit.Status.ToString(), timeProvider.GetUtcNow()),
            cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return Map(unit);
    }

    public async Task<IReadOnlyCollection<ProductUnitResponse>> CreateUnitsAsync(
        CreateProductUnitsCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Quantity is < 1 or > 200)
            throw new DomainException("product_unit.quantity_invalid", "Tek işlemde 1 ile 200 arasında fiziksel kit oluşturulabilir.");
        if (await repository.GetProductModelAsync(command.ProductModelId, cancellationToken) is null)
            throw new ResourceNotFoundException("Ürün modeli bulunamadı.");

        var now = timeProvider.GetUtcNow();
        var units = new List<ProductUnit>(command.Quantity);
        for (var index = 0; index < command.Quantity; index++)
        {
            var serialNumber = GenerateSerialNumber(now);
            var unit = ProductUnit.Create(Guid.NewGuid(), command.ProductModelId, serialNumber,
                $"KITRENTAL:{serialNumber}", command.ActorId, now);
            try
            {
                await repository.AddProductUnitAsync(unit, cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                throw new ConflictException("product_unit.identifier_not_unique", exception.Message);
            }
            await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), command.ActorId, nameof(ProductUnit),
                unit.Id, "Created", null, unit.Status.ToString(), now), cancellationToken);
            units.Add(unit);
        }
        await repository.SaveChangesAsync(cancellationToken);
        return units.Select(Map).ToArray();
    }

    public async Task<IReadOnlyCollection<ProductUnitResponse>> GetUnitsAsync(CancellationToken cancellationToken) =>
        (await repository.GetProductUnitsAsync(cancellationToken)).Select(Map).ToArray();

    private static ProductUnitResponse Map(ProductUnit unit) =>
        new(unit.Id, unit.ProductModelId, unit.SerialNumber, unit.QrCode, unit.Status);

    private static string GenerateSerialNumber(DateTimeOffset now) =>
        $"KR-{now:yyyyMMdd}-{Guid.NewGuid():N}".ToUpperInvariant();

    private static ProductModelResponse MapModel(ProductModel model) =>
        new(model.Id, model.Name, model.Sku, model.Description, model.ImageUrl);
}
