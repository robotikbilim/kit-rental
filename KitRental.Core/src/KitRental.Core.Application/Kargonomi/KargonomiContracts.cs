using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Support;

namespace KitRental.Core.Application.Kargonomi;

public sealed record KargonomiCreateShipmentRequest(
    string BuyerName,
    string BuyerPhone,
    string BuyerAddress,
    int BuyerStateId,
    int BuyerCityId,
    string PackageContent,
    string PackageBarcode,
    decimal PackageDesi);

public sealed record KargonomiShipmentSnapshot(
    int Id,
    string? Status,
    string? StatusLabel,
    string? Carrier,
    string? TrackingNumber,
    string? Barcode,
    DateTimeOffset? UpdatedAt);

public sealed record KargonomiCarrierQuote(int Id, string Name, string Slug, string? Price);

public interface IKargonomiClient
{
    Task<KargonomiShipmentSnapshot> CreateShipmentAsync(KargonomiCreateShipmentRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<KargonomiCarrierQuote>> GetPriceQuotesAsync(int shipmentId, CancellationToken cancellationToken);
    Task<KargonomiShipmentSnapshot> ConfirmShippingPriceAsync(int shipmentId, int providerId, CancellationToken cancellationToken);
    Task<KargonomiShipmentSnapshot> GetShipmentAsync(int shipmentId, CancellationToken cancellationToken);
    Task<string> GetBarcodeAsync(int shipmentId, CancellationToken cancellationToken);
    Task<(int StateId, int CityId)> ResolveLocationAsync(string address, CancellationToken cancellationToken);
}

public sealed record KargonomiShipmentResponse(
    Guid Id,
    Guid OrderId,
    Guid StudentId,
    int? ExternalShipmentId,
    string StudentName,
    string Address,
    string Carrier,
    string? TrackingNumber,
    string? ExternalStatus,
    string StatusLabel,
    KargonomiShipmentState State,
    string? LastError,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<KargonomiShipmentEventResponse> Events);

public sealed record KargonomiShipmentEventResponse(
    string ExternalStatus,
    string StatusLabel,
    KargonomiShipmentState State,
    string? TrackingNumber,
    DateTimeOffset OccurredAt,
    string? Description);

public sealed record KargonomiShipmentAttemptResponse(Guid StudentId, string StudentName, bool Succeeded,
    string Message, KargonomiShipmentResponse? Shipment);

public sealed record KargonomiShipmentBatchResponse(
    IReadOnlyCollection<KargonomiShipmentAttemptResponse> Items,
    int SucceededCount,
    int FailedCount);

public sealed record FaultKargonomiShipmentResponse(Guid Id, Guid FaultTicketId,
    FaultKargonomiShipmentDirection Direction, int? ExternalShipmentId, string RecipientName,
    string RecipientAddress, string? TrackingNumber, string Carrier, string StatusLabel,
    KargonomiShipmentState State, string? LastError, DateTimeOffset UpdatedAt);

public sealed record KargonomiShipmentListItemResponse(
    Guid Id, Guid OrderId, string OrderNumber, Guid StudentId, string StudentName,
    string? TrackingNumber, string Carrier, string StatusLabel, KargonomiShipmentState State,
    string? LastError, DateTimeOffset UpdatedAt);
