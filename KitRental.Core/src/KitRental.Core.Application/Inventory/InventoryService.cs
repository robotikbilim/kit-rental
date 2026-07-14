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

    public async Task<ProductModelResponse> UpdateModelAsync(UpdateProductModelCommand command, CancellationToken cancellationToken)
    {
        var model = await repository.GetProductModelAsync(command.Id, cancellationToken)
            ?? throw new ResourceNotFoundException("Eğitim kiti bulunamadı.");
        if ((await repository.GetProductModelsAsync(cancellationToken)).Any(item => item.Id != model.Id &&
            item.Sku.Equals(command.Sku.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException("product_model.sku_not_unique", "Bu SKU başka bir eğitim kitinde kullanılıyor.");
        var previous = model.Sku;
        model.Update(command.Name, command.Sku, command.Description, command.ImageUrl);
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), command.ActorId, nameof(ProductModel),
            model.Id, "Updated", previous, model.Sku, timeProvider.GetUtcNow()), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return MapModel(model);
    }

    public async Task DeleteModelAsync(Guid id, Guid actorId, CancellationToken cancellationToken)
    {
        var model = await repository.GetProductModelAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("Eğitim kiti bulunamadı.");
        if ((await repository.GetProductUnitsAsync(cancellationToken)).Any(item => item.ProductModelId == id))
            throw new ConflictException("product_model.in_use", "Fiziksel kitleri bulunan eğitim seti silinemez.");
        if ((await repository.GetOrdersAsync(null, cancellationToken)).Any(order => order.Lines.Any(line => line.ProductModelId == id)))
            throw new ConflictException("product_model.in_use", "Siparişlerde kullanılan eğitim seti silinemez.");
        await repository.RemoveProductModelAsync(model, cancellationToken);
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), actorId, nameof(ProductModel), id,
            "Deleted", model.Sku, null, timeProvider.GetUtcNow()), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
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

    public async Task<InventoryPageResponse> GetInventoryAsync(string? query, Guid? productModelId,
        ProductUnitStatus? status, DateOnly? createdFrom, DateOnly? createdTo, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var models = (await repository.GetProductModelsAsync(cancellationToken)).ToDictionary(item => item.Id);
        var normalizedQuery = query?.Trim() ?? string.Empty;
        var items = (await repository.GetProductUnitsAsync(cancellationToken))
            .Where(unit => models.ContainsKey(unit.ProductModelId))
            .Select(unit =>
            {
                var model = models[unit.ProductModelId];
                var createdAt = unit.History.OrderBy(item => item.OccurredAt).FirstOrDefault()?.OccurredAt
                    ?? DateTimeOffset.MinValue;
                return new InventoryItemResponse(unit.Id, unit.ProductModelId, model.Name, model.Sku,
                    unit.SerialNumber, unit.QrCode, unit.Status, createdAt);
            })
            .Where(item => !productModelId.HasValue || item.ProductModelId == productModelId.Value)
            .Where(item => !status.HasValue || item.Status == status.Value)
            .Where(item => normalizedQuery.Length == 0 ||
                item.SerialNumber.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                item.QrCode.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                item.ProductModelName.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase) ||
                item.ProductModelSku.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Where(item => !createdFrom.HasValue || DateOnly.FromDateTime(item.CreatedAt.LocalDateTime) >= createdFrom.Value)
            .Where(item => !createdTo.HasValue || DateOnly.FromDateTime(item.CreatedAt.LocalDateTime) <= createdTo.Value)
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.SerialNumber)
            .ToArray();

        var validPageSize = Math.Clamp(pageSize, 10, 100);
        var totalPages = Math.Max(1, (int)Math.Ceiling(items.Length / (double)validPageSize));
        var validPage = Math.Clamp(page, 1, totalPages);
        return new InventoryPageResponse(validPage, validPageSize, items.Length, totalPages,
            items.Skip((validPage - 1) * validPageSize).Take(validPageSize).ToArray());
    }

    public async Task<ProductUnitResponse> UpdateUnitAsync(UpdateProductUnitCommand command, CancellationToken cancellationToken)
    {
        var unit = await repository.GetProductUnitAsync(command.Id, cancellationToken)
            ?? throw new ResourceNotFoundException("Fiziksel kit bulunamadı.");
        var normalizedSerial = command.SerialNumber.Trim().ToUpperInvariant();
        var normalizedQr = command.QrCode.Trim().ToUpperInvariant();
        if ((await repository.GetProductUnitsAsync(cancellationToken)).Any(item => item.Id != unit.Id &&
            (item.SerialNumber == normalizedSerial || item.QrCode == normalizedQr)))
            throw new ConflictException("product_unit.identifier_not_unique", "Seri numarası veya QR kod başka bir fiziksel kitte kullanılıyor.");
        var previous = $"{unit.SerialNumber}|{unit.QrCode}";
        unit.UpdateIdentifiers(command.SerialNumber, command.QrCode);
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), command.ActorId, nameof(ProductUnit), unit.Id,
            "Updated", previous, $"{unit.SerialNumber}|{unit.QrCode}", timeProvider.GetUtcNow()), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(unit);
    }

    public async Task DeleteUnitAsync(Guid id, Guid actorId, CancellationToken cancellationToken)
    {
        var unit = await repository.GetProductUnitAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("Fiziksel kit bulunamadı.");
        if (unit.Status != ProductUnitStatus.Available ||
            (await repository.GetAssignmentsForProductUnitAsync(id, cancellationToken)).Count > 0 ||
            (await repository.GetFaultTicketsAsync(null, cancellationToken)).Any(item => item.ProductUnitId == id))
            throw new ConflictException("product_unit.in_use", "Yalnızca hiç kiralanmamış, arızası olmayan ve kiralanabilir durumdaki fiziksel kit silinebilir.");
        await repository.RemoveProductUnitAsync(unit, cancellationToken);
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), actorId, nameof(ProductUnit), id,
            "Deleted", unit.SerialNumber, null, timeProvider.GetUtcNow()), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private static ProductUnitResponse Map(ProductUnit unit) =>
        new(unit.Id, unit.ProductModelId, unit.SerialNumber, unit.QrCode, unit.Status);

    private static string GenerateSerialNumber(DateTimeOffset now) =>
        $"KR-{now:yyyyMMdd}-{Guid.NewGuid():N}".ToUpperInvariant();

    private static ProductModelResponse MapModel(ProductModel model) =>
        new(model.Id, model.Name, model.Sku, model.Description, model.ImageUrl);
}
