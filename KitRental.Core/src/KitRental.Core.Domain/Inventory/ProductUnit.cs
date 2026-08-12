using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Inventory;

public sealed class ProductUnit
{
    private readonly List<InventoryEvent> _history = [];

    private ProductUnit()
    {
    }

    private ProductUnit(Guid id, Guid productModelId, string serialNumber, string qrCode)
    {
        Id = id;
        ProductModelId = productModelId;
        SerialNumber = serialNumber;
        QrCode = qrCode;
        Status = ProductUnitStatus.Available;
    }

    public Guid Id { get; private set; }
    public Guid ProductModelId { get; private set; }
    public string SerialNumber { get; private set; } = string.Empty;
    public string QrCode { get; private set; } = string.Empty;
    public ProductUnitStatus Status { get; private set; }
    public IReadOnlyCollection<InventoryEvent> History => _history.AsReadOnly();

    public static ProductUnit Create(
        Guid id,
        Guid productModelId,
        string serialNumber,
        string qrCode,
        Guid actorId,
        DateTimeOffset occurredAt)
    {
        if (id == Guid.Empty || productModelId == Guid.Empty || actorId == Guid.Empty)
            throw new DomainException("product_unit.id_required", "Product unit, product model and actor id are required.");

        if (string.IsNullOrWhiteSpace(serialNumber) || string.IsNullOrWhiteSpace(qrCode))
            throw new DomainException("product_unit.identifiers_required", "Serial number and QR code are required.");

        var unit = new ProductUnit(id, productModelId, serialNumber.Trim().ToUpperInvariant(), qrCode.Trim().ToUpperInvariant());
        unit._history.Add(new InventoryEvent(Guid.NewGuid(), id, null, ProductUnitStatus.Available, occurredAt, actorId, "Physical product unit created."));
        return unit;
    }

    public void UpdateIdentifiers(string serialNumber, string qrCode)
    {
        if (string.IsNullOrWhiteSpace(serialNumber) || string.IsNullOrWhiteSpace(qrCode))
            throw new DomainException("product_unit.identifiers_required", "Serial number and QR code are required.");
        SerialNumber = serialNumber.Trim().ToUpperInvariant();
        QrCode = qrCode.Trim().ToUpperInvariant();
    }

    public void Reserve(Guid actorId, DateTimeOffset occurredAt) =>
        TransitionTo(ProductUnitStatus.Reserved, actorId, occurredAt, "Reserved for rental.", ProductUnitStatus.Available);

    public void ReleaseReservation(Guid actorId, DateTimeOffset occurredAt) =>
        TransitionTo(ProductUnitStatus.Available, actorId, occurredAt, "Reservation released.", ProductUnitStatus.Reserved);

    public void StartPreparation(Guid actorId, DateTimeOffset occurredAt) =>
        TransitionTo(ProductUnitStatus.Preparing, actorId, occurredAt, "Kit assigned for shipping.", ProductUnitStatus.Reserved);

    public void Dispatch(Guid actorId, DateTimeOffset occurredAt) =>
        TransitionTo(ProductUnitStatus.OutboundInTransit, actorId, occurredAt, "Dispatched for outbound shipping.", ProductUnitStatus.Preparing);

    public void ConfirmDelivery(Guid actorId, DateTimeOffset occurredAt) =>
        TransitionTo(ProductUnitStatus.WithCustomer, actorId, occurredAt, "Delivery confirmed.", ProductUnitStatus.OutboundInTransit);

    public void ConfirmDeliveryTo(Guid actorId, DateTimeOffset occurredAt, string recipientName, string address)
    {
        var reason = $"Delivery confirmed. Recipient: {recipientName.Trim()}. Address: {address.Trim()}";
        if (reason.Length > 500) reason = reason[..497] + "...";
        TransitionTo(ProductUnitStatus.WithCustomer, actorId, occurredAt, reason, ProductUnitStatus.OutboundInTransit);
    }

    public void CompleteSale(Guid actorId, DateTimeOffset occurredAt) =>
        TransitionTo(ProductUnitStatus.Sold, actorId, occurredAt, "Sale delivery completed; kit removed from rental fleet.",
            ProductUnitStatus.OutboundInTransit);

    public void StartReturn(Guid actorId, DateTimeOffset occurredAt) =>
        TransitionTo(ProductUnitStatus.ReturnInTransit, actorId, occurredAt, "Sent to return shipping.", ProductUnitStatus.WithCustomer);

    public void ReceiveForInspection(Guid actorId, DateTimeOffset occurredAt) =>
        TransitionTo(ProductUnitStatus.UnderInspection, actorId, occurredAt, "Received to warehouse; waiting for inspection.", ProductUnitStatus.ReturnInTransit);

    public void CompleteInspection(ProductUnitStatus outcome, Guid actorId, DateTimeOffset occurredAt, string reason)
    {
        if (outcome is not (ProductUnitStatus.Available or ProductUnitStatus.InMaintenance or ProductUnitStatus.Quarantined or ProductUnitStatus.Retired))
            throw new DomainException("product_unit.invalid_inspection_outcome", "Invalid return inspection outcome.");

        TransitionTo(outcome, actorId, occurredAt, reason, ProductUnitStatus.UnderInspection);
    }

    public void ReceiveReturnToAvailable(Guid actorId, DateTimeOffset occurredAt)
    {
        ReceiveForInspection(actorId, occurredAt);
        CompleteInspection(ProductUnitStatus.Available, actorId, occurredAt, "Customer return received; kit is available again.");
    }

    private void TransitionTo(
        ProductUnitStatus next,
        Guid actorId,
        DateTimeOffset occurredAt,
        string reason,
        params ProductUnitStatus[] allowedCurrentStatuses)
    {
        if (!allowedCurrentStatuses.Contains(Status))
            throw new DomainException("product_unit.invalid_status_transition", $"Product unit cannot move from {Status} to {next}.");

        if (actorId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
            throw new DomainException("inventory_event.audit_data_required", "Actor and reason are required for status changes.");

        var previous = Status;
        Status = next;
        _history.Add(new InventoryEvent(Guid.NewGuid(), Id, previous, next, occurredAt, actorId, reason.Trim()));
    }
}
