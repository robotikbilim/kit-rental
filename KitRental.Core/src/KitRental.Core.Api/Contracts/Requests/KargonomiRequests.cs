namespace KitRental.Core.Api.Contracts.Requests;

public sealed record KargonomiShipmentStartRequest(IReadOnlyCollection<Guid>? StudentIds);
