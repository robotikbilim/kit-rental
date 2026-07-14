using KitRental.Core.Domain.Inventory;

namespace KitRental.Core.Application.Inventory;

public sealed record CreateProductModelCommand(string Name, string Sku, string? Description, string? ImageUrl, Guid ActorId);

public sealed record CreateProductUnitCommand(Guid ProductModelId, string? SerialNumber, string? QrCode, Guid ActorId);
public sealed record CreateProductUnitsCommand(Guid ProductModelId, int Quantity, Guid ActorId);

public sealed record ProductModelResponse(Guid Id, string Name, string Sku, string? Description, string? ImageUrl);

public sealed record ProductUnitResponse(
    Guid Id,
    Guid ProductModelId,
    string SerialNumber,
    string QrCode,
    ProductUnitStatus Status);
