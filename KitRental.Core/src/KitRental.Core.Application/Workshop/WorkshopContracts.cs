using KitRental.Core.Domain.Warehouse;

namespace KitRental.Core.Application.Workshop;

public sealed record CreateComponentCommand(
    string Name,
    string Sku,
    string UnitOfMeasure,
    decimal MinimumStock,
    string? ImageUrl,
    Guid ActorId);

public sealed record CreateStorageLocationCommand(
    string Code,
    string Warehouse,
    string Aisle,
    string Rack,
    string Shelf,
    Guid ActorId);

public sealed record RecordStockCommand(
    Guid ComponentId,
    Guid StorageLocationId,
    decimal Quantity,
    string Reference,
    Guid ActorId);

public sealed record TransferStockCommand(
    Guid ComponentId,
    Guid FromStorageLocationId,
    Guid ToStorageLocationId,
    decimal Quantity,
    string Reference,
    Guid ActorId);

public sealed record CreateBillOfMaterialsCommand(
    Guid ProductModelId,
    int Version,
    IReadOnlyCollection<BillOfMaterialsLineCommand> Lines,
    Guid ActorId);

public sealed record CreateKitCommand(
    string Name,
    string Sku,
    string? Description,
    string? ImageUrl,
    int BomVersion,
    IReadOnlyCollection<BillOfMaterialsLineCommand> Lines,
    Guid ActorId);

public sealed record BillOfMaterialsLineCommand(Guid ComponentId, decimal Quantity);

public sealed record ComponentResponse(
    Guid Id,
    string Name,
    string Sku,
    string UnitOfMeasure,
    decimal MinimumStock,
    string? ImageUrl,
    decimal TotalStock,
    bool IsLowStock);

public sealed record ComponentSearchResponse(
    Guid Id,
    string Name,
    string Sku,
    string? ImageUrl,
    decimal TotalStock,
    string UnitOfMeasure);

public sealed record ComponentLocationResponse(
    Guid StorageLocationId,
    string LocationCode,
    string Warehouse,
    string Aisle,
    string Rack,
    string Shelf,
    decimal Quantity);

public sealed record ComponentLocatorResponse(
    Guid Id,
    string Name,
    string Sku,
    string UnitOfMeasure,
    string? ImageUrl,
    decimal TotalStock,
    decimal MinimumStock,
    bool IsLowStock,
    IReadOnlyCollection<ComponentLocationResponse> Locations);

public sealed record StorageLocationResponse(
    Guid Id,
    string Code,
    string Warehouse,
    string Aisle,
    string Rack,
    string Shelf);

public sealed record ComponentStockResponse(
    Guid ComponentId,
    string ComponentName,
    string ComponentSku,
    Guid StorageLocationId,
    string LocationCode,
    decimal Quantity,
    string UnitOfMeasure);

public sealed record StockMovementResponse(
    Guid Id,
    Guid ComponentId,
    Guid StorageLocationId,
    StockMovementType Type,
    decimal Quantity,
    string Reference,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    Guid? TransferId);

public sealed record BillOfMaterialsLineResponse(
    Guid ComponentId,
    string ComponentName,
    string ComponentSku,
    decimal Quantity,
    string UnitOfMeasure);

public sealed record BillOfMaterialsResponse(
    Guid Id,
    Guid ProductModelId,
    string ProductName,
    string ProductSku,
    int Version,
    IReadOnlyCollection<BillOfMaterialsLineResponse> Lines);

public sealed record KitCatalogResponse(
    Guid Id,
    string Name,
    string Sku,
    string? Description,
    string? ImageUrl,
    int BomVersion,
    IReadOnlyCollection<BillOfMaterialsLineResponse> Lines);

public sealed record BuildableComponentResponse(
    Guid ComponentId,
    string ComponentName,
    string ComponentSku,
    string UnitOfMeasure,
    string? ImageUrl,
    decimal RequiredPerKit,
    decimal AvailableStock,
    int SupportsKitCount,
    bool IsBottleneck,
    bool IsLowStock,
    decimal MissingForNextKit);

public sealed record BuildableKitResponse(
    Guid ProductModelId,
    string ProductName,
    string ProductSku,
    string? ProductImageUrl,
    int BomVersion,
    int BuildableQuantity,
    IReadOnlyCollection<BuildableComponentResponse> Components);
