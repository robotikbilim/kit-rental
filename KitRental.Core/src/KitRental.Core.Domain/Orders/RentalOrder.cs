using KitRental.Core.Domain.Customers;
using KitRental.Core.Domain.Rentals;
using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Orders;

public enum RentalOrderStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Preparing = 4,
    ReadyToShip = 5,
    OutboundInTransit = 6,
    Delivered = 7,
    RentalActive = 8,
    AwaitingReturn = 9,
    ReturnInTransit = 10,
    UnderInspection = 11,
    DamageReview = 12,
    Completed = 13,
    Rejected = 14,
    Cancelled = 15,
    DeliveryIssue = 16,
    Overdue = 17
}

public sealed record RentalOrderLine(Guid Id, Guid ProductModelId, int Quantity);
public sealed record OrderStatusEvent(Guid Id, RentalOrderStatus Previous, RentalOrderStatus Current, DateTimeOffset OccurredAt, Guid ActorId, string Reason);

public sealed class RentalOrder
{
    private readonly List<RentalOrderLine> _lines = [];
    private readonly List<OrderStatusEvent> _history = [];

    private RentalOrder()
    {
    }

    private RentalOrder(Guid id, string orderNumber, Guid customerId, RentalPeriod period, AddressSnapshot address, DateTimeOffset createdAt)
    {
        Id = id;
        OrderNumber = orderNumber;
        CustomerId = customerId;
        Period = period;
        DeliveryAddress = address;
        CreatedAt = createdAt;
        Status = RentalOrderStatus.Draft;
    }

    public Guid Id { get; private set; }
    public string OrderNumber { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public RentalPeriod Period { get; private set; }
    public AddressSnapshot DeliveryAddress { get; private set; } = null!;
    public RentalOrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyCollection<RentalOrderLine> Lines => _lines.AsReadOnly();
    public IReadOnlyCollection<OrderStatusEvent> History => _history.AsReadOnly();

    public static RentalOrder Create(Guid id, string orderNumber, Guid customerId, RentalPeriod period, AddressSnapshot address, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || customerId == Guid.Empty || string.IsNullOrWhiteSpace(orderNumber))
        {
            throw new DomainException("order.required_fields", "Sipariş kimliği, numarası ve müşteri zorunludur.");
        }

        return new RentalOrder(id, orderNumber.Trim().ToUpperInvariant(), customerId, period, address, createdAt);
    }

    public RentalOrderLine AddLine(Guid productModelId, int quantity)
    {
        if (productModelId == Guid.Empty || quantity <= 0)
        {
            throw new DomainException("order_line.invalid", "Ürün modeli ve pozitif adet zorunludur.");
        }

        var line = new RentalOrderLine(Guid.NewGuid(), productModelId, quantity);
        _lines.Add(line);
        return line;
    }

    public void ReplaceLines(IReadOnlyCollection<(Guid ProductModelId, int Quantity)> lines)
    {
        if (Status != RentalOrderStatus.Approved || lines.Count == 0)
            throw new DomainException("order.lines_not_editable", "Yalnızca onaylanmış ve henüz hazırlanmamış siparişin kitleri düzenlenebilir.");
        if (lines.Any(line => line.ProductModelId == Guid.Empty || line.Quantity <= 0))
            throw new DomainException("order_line.invalid", "Ürün modeli ve pozitif adet zorunludur.");
        _lines.Clear();
        foreach (var line in lines)
            _lines.Add(new RentalOrderLine(Guid.NewGuid(), line.ProductModelId, line.Quantity));
    }

    public void Submit(Guid actorId, DateTimeOffset now) => Transition(RentalOrderStatus.PendingApproval, actorId, now, "Onaya gönderildi.", RentalOrderStatus.Draft);
    public void Approve(Guid actorId, DateTimeOffset now) => Transition(RentalOrderStatus.Approved, actorId, now, "Sipariş onaylandı.", RentalOrderStatus.PendingApproval);
    public void StartPreparation(Guid actorId, DateTimeOffset now) => Transition(RentalOrderStatus.Preparing, actorId, now, "Hazırlık başladı.", RentalOrderStatus.Approved);
    public void MarkReadyToShip(Guid actorId, DateTimeOffset now) => Transition(RentalOrderStatus.ReadyToShip, actorId, now, "Kargoya hazır.", RentalOrderStatus.Preparing);
    public void Dispatch(Guid actorId, DateTimeOffset now) => Transition(RentalOrderStatus.OutboundInTransit, actorId, now, "Çıkış kargosunda.", RentalOrderStatus.ReadyToShip);
    public void ConfirmDelivery(Guid actorId, DateTimeOffset now) => Transition(RentalOrderStatus.Delivered, actorId, now, "Teslimat doğrulandı.", RentalOrderStatus.OutboundInTransit);
    public void LockAfterDelivery(Guid actorId, DateTimeOffset now) => Transition(RentalOrderStatus.Completed, actorId, now, "Teslimat tamamlandı; sipariş kilitlendi.", RentalOrderStatus.Delivered);
    public void ActivateRental(Guid actorId, DateTimeOffset now) => Transition(RentalOrderStatus.RentalActive, actorId, now, "Kiralama aktifleştirildi.", RentalOrderStatus.Delivered);
    public void RequestReturn(Guid actorId, DateTimeOffset now) => Transition(RentalOrderStatus.AwaitingReturn, actorId, now, "İade süreci başlatıldı.", RentalOrderStatus.RentalActive, RentalOrderStatus.Overdue);
    public void StartReturnShipment(Guid actorId, DateTimeOffset now) => Transition(RentalOrderStatus.ReturnInTransit, actorId, now, "İade kargosunda.", RentalOrderStatus.AwaitingReturn);
    public void ReceiveReturn(Guid actorId, DateTimeOffset now) => Transition(RentalOrderStatus.UnderInspection, actorId, now, "İade kontrolünde.", RentalOrderStatus.ReturnInTransit);
    public void Complete(Guid actorId, DateTimeOffset now) => Transition(RentalOrderStatus.Completed, actorId, now, "Sipariş kapatıldı.", RentalOrderStatus.UnderInspection, RentalOrderStatus.DamageReview);

    private void Transition(RentalOrderStatus next, Guid actorId, DateTimeOffset now, string reason, params RentalOrderStatus[] allowed)
    {
        if (actorId == Guid.Empty || !allowed.Contains(Status))
        {
            throw new DomainException("order.invalid_status_transition", $"Sipariş {Status} durumundan {next} durumuna geçirilemez.");
        }

        var previous = Status;
        Status = next;
        _history.Add(new OrderStatusEvent(Guid.NewGuid(), previous, next, now, actorId, reason));
    }
}
