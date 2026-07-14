using KitRental.Core.Domain.Procurement;

namespace KitRental.Core.Application.Procurement;

public sealed record SupplyNeedLineCommand(Guid ComponentId, decimal Quantity);
public sealed record CreateSupplyNeedCommand(IReadOnlyCollection<SupplyNeedLineCommand> Lines, Guid ActorId);
public sealed record UpdateSupplyNeedCommand(Guid Id, IReadOnlyCollection<SupplyNeedLineCommand> Lines, Guid ActorId);
public sealed record CompleteSupplyNeedCommand(Guid Id, Guid StorageLocationId,
    IReadOnlyCollection<SupplyNeedLineCommand> Lines, Guid ActorId);
public sealed record SupplyNeedLineResponse(Guid ComponentId, string ComponentName, string ComponentSku,
    string UnitOfMeasure, decimal Quantity, decimal? SuppliedQuantity);
public sealed record SupplyNeedResponse(Guid Id, SupplyNeedStatus Status, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, IReadOnlyCollection<SupplyNeedLineResponse> Lines);
