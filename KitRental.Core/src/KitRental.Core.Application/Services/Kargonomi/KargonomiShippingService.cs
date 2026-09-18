using KitRental.Core.Application.Abstractions;
using KitRental.Core.Application.Common;
using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Support;
using KitRental.SharedKernel;

namespace KitRental.Core.Application.Kargonomi;

public sealed class KargonomiShippingService(
    ICoreRepository repository,
    IKargonomiClient client,
    TimeProvider timeProvider)
{
    private static readonly Guid SystemActorId = new("00000000-0000-0000-0000-000000000002");

    public async Task<FaultKargonomiShipmentResponse> StartForFaultAsync(Guid faultTicketId,
        FaultKargonomiShipmentDirection direction, string recipientName, string recipientPhone,
        string recipientAddress, CancellationToken cancellationToken)
    {
        var ticket = await repository.GetFaultTicketAsync(faultTicketId, cancellationToken)
            ?? throw new ResourceNotFoundException("Arıza kaydı bulunamadı.");
        var existing = ticket.KargonomiShipments.SingleOrDefault(item => item.Direction == direction && item.State != KargonomiShipmentState.Failed);
        if (existing is not null) return MapFault(existing);
        var shipment = ticket.CreateKargonomiShipment(direction, recipientName, recipientPhone, recipientAddress, timeProvider.GetUtcNow());
        try
        {
            var location = await client.ResolveLocationAsync(recipientAddress, cancellationToken);
            var created = await client.CreateShipmentAsync(new KargonomiCreateShipmentRequest(
                recipientName, recipientPhone, recipientAddress, location.StateId, location.CityId,
                $"Arıza kiti {ticket.Number}", ticket.Number, 1), cancellationToken);
            var quotes = await client.GetPriceQuotesAsync(created.Id, cancellationToken);
            var aras = quotes.FirstOrDefault(item => item.Slug.Equals("aras", StringComparison.OrdinalIgnoreCase) || item.Name.Contains("Aras", StringComparison.OrdinalIgnoreCase));
            if (aras is null) throw new ConflictException("kargonomi.aras_quote_missing", "Aras Kargo için uygun fiyat teklifi bulunamadı.");
            var confirmed = await client.ConfirmShippingPriceAsync(created.Id, aras.Id, cancellationToken);
            shipment.MarkCreated(confirmed.Id, confirmed.Status, confirmed.StatusLabel ?? "Hazır", confirmed.TrackingNumber, timeProvider.GetUtcNow());
            if (direction == FaultKargonomiShipmentDirection.ToWorkshop)
                ticket.MarkWorkshopShipmentInTransit(SystemActorId, timeProvider.GetTurkeyNow(), "Kargonomi ile atölyeye gönderildi.");
            else
                ticket.MarkCustomerShipmentInTransit(SystemActorId, timeProvider.GetTurkeyNow(), "Kargonomi ile müşteriye geri gönderildi.");
            await repository.SaveChangesAsync(cancellationToken);
            return MapFault(shipment);
        }
        catch (Exception exception) when (exception is ConflictException or HttpRequestException or TaskCanceledException)
        {
            shipment.MarkFailed(exception.Message, timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyCollection<FaultKargonomiShipmentResponse>> GetForFaultAsync(Guid faultTicketId, CancellationToken cancellationToken)
    {
        var ticket = await repository.GetFaultTicketAsync(faultTicketId, cancellationToken) ?? throw new ResourceNotFoundException("Arıza kaydı bulunamadı.");
        return ticket.KargonomiShipments.Select(MapFault).ToArray();
    }

    public async Task ApplyFaultWebhookAsync(int externalShipmentId, string? status, string? statusLabel,
        string? trackingNumber, string? description, CancellationToken cancellationToken)
    {
        var ticket = (await repository.GetFaultTicketsAsync(null, cancellationToken))
            .FirstOrDefault(item => item.KargonomiShipments.Any(shipment => shipment.ExternalShipmentId == externalShipmentId))
            ?? throw new ResourceNotFoundException("Arıza Kargonomi gönderisi eşleşmedi.");
        var shipment = ticket.KargonomiShipments.First(item => item.ExternalShipmentId == externalShipmentId);
        shipment.ApplyUpdate(status, statusLabel, trackingNumber, timeProvider.GetUtcNow(), description);
        ApplyFaultDeliveryTransition(ticket, shipment);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private static FaultKargonomiShipmentResponse MapFault(FaultKargonomiShipment shipment) =>
        new(shipment.Id, shipment.FaultTicketId, shipment.Direction, shipment.ExternalShipmentId, shipment.RecipientName,
            shipment.RecipientAddress, shipment.TrackingNumber, shipment.Carrier, shipment.StatusLabel, shipment.State,
            shipment.LastError, shipment.UpdatedAt);
    public async Task<KargonomiShipmentBatchResponse> StartForOrderAsync(Guid orderId,
        IReadOnlyCollection<Guid>? studentIds, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(orderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Sipariş bulunamadı.");
        if (order.Type != OrderType.Rental)
            throw new ConflictException("kargonomi.rental_only", "Kargonomi öğrenci gönderisi yalnızca kiralama siparişlerinde kullanılabilir.");

        var cohort = (await repository.GetRentalCohortsAsync(order.CustomerId, cancellationToken))
            .FirstOrDefault(item => item.Students.Any(student => student.OrderId == orderId));
        var students = cohort?.Students.Where(item => !item.IsDeleted && item.OrderId == orderId &&
                (studentIds is null || studentIds.Contains(item.Id))).ToArray() ?? [];
        var attempts = new List<KargonomiShipmentAttemptResponse>();

        foreach (var student in students)
        {
            KargonomiShipment? shipment = null;
            try
            {
                shipment = await repository.GetKargonomiShipmentAsync(orderId, student.Id, cancellationToken);
                if (shipment is not null && shipment.ExternalShipmentId.HasValue)
                {
                    attempts.Add(new(student.Id, student.FullName, true, "Kargo zaten başlatılmış.", Map(shipment, student.FullName)));
                    continue;
                }
                if (!student.HasAddress)
                    throw new ConflictException("kargonomi.address_required", "Öğrenci adresi tamamlanmadan kargo başlatılamaz.");

                if (shipment is null)
                {
                    shipment = KargonomiShipment.Create(Guid.NewGuid(), orderId, student.Id,
                        timeProvider.GetUtcNow());
                    await repository.AddKargonomiShipmentAsync(shipment, cancellationToken);
                }

                var location = await client.ResolveLocationAsync(student.AddressLine, cancellationToken);
                var created = await client.CreateShipmentAsync(new KargonomiCreateShipmentRequest(
                    student.FullName, student.GuardianPhone, student.AddressLine, location.StateId, location.CityId,
                    "Eğitim kiti", string.Empty, 1), cancellationToken);
                var quotes = await client.GetPriceQuotesAsync(created.Id, cancellationToken);
                var aras = quotes.FirstOrDefault(item => item.Slug.Equals("aras", StringComparison.OrdinalIgnoreCase) ||
                    item.Name.Contains("Aras", StringComparison.OrdinalIgnoreCase));
                if (aras is null)
                    throw new ConflictException("kargonomi.aras_quote_missing", "Aras Kargo için uygun fiyat teklifi bulunamadı.");

                var confirmed = await client.ConfirmShippingPriceAsync(created.Id, aras.Id, cancellationToken);
                shipment.MarkCreated(confirmed.Id, confirmed.Status, confirmed.StatusLabel ?? "Hazır",
                    confirmed.TrackingNumber, timeProvider.GetUtcNow());
                await repository.SaveChangesAsync(cancellationToken);
                attempts.Add(new(student.Id, student.FullName, true, "Kargo başlatıldı.", Map(shipment, student.FullName)));
            }
            catch (Exception exception) when (exception is ConflictException or HttpRequestException or TaskCanceledException)
            {
                if (shipment is null)
                {
                    shipment = KargonomiShipment.Create(Guid.NewGuid(), orderId, student.Id, timeProvider.GetUtcNow());
                    await repository.AddKargonomiShipmentAsync(shipment, cancellationToken);
                }
                shipment.MarkFailed(exception.Message, timeProvider.GetUtcNow());
                await repository.SaveChangesAsync(cancellationToken);
                attempts.Add(new(student.Id, student.FullName, false,
                    shipment.LastError ?? "Bilinmeyen Kargonomi hatası.", Map(shipment, student.FullName)));
            }
        }

        return new(attempts, attempts.Count(item => item.Succeeded), attempts.Count(item => !item.Succeeded));
    }

    public async Task<IReadOnlyCollection<KargonomiShipmentResponse>> GetForOrderAsync(Guid orderId,
        CancellationToken cancellationToken)
    {
        var students = (await repository.GetRentalCohortsAsync(null, cancellationToken)).SelectMany(item => item.Students)
            .Where(item => item.OrderId == orderId).ToDictionary(item => item.Id);
        return (await repository.GetKargonomiShipmentsAsync(orderId, cancellationToken))
            .Select(item => Map(item, students.GetValueOrDefault(item.StudentId)?.FullName ?? "Öğrenci")).ToArray();
    }

    public async Task<IReadOnlyCollection<KargonomiShipmentListItemResponse>> GetAllAsync(CancellationToken cancellationToken)
        => (await client.GetShipmentsAsync(cancellationToken))
            .OrderByDescending(item => item.CreatedAt)
            .Select(MapListItem)
            .ToArray();

    public async Task<KargonomiShipmentRefreshResponse> RefreshAllAsync(CancellationToken cancellationToken)
    {
        var snapshots = await client.GetShipmentsAsync(cancellationToken);
        var snapshotsById = snapshots
            .Where(item => item.Id > 0)
            .GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.Last());
        var occurredAt = timeProvider.GetUtcNow();

        var orderShipmentCount = 0;
        var orderShipments = await repository.GetKargonomiShipmentsAsync(null, cancellationToken);
        foreach (var shipment in orderShipments)
        {
            if (!shipment.ExternalShipmentId.HasValue ||
                !snapshotsById.TryGetValue(shipment.ExternalShipmentId.Value, out var snapshot))
                continue;

            shipment.ApplyUpdate(snapshot.Status, snapshot.StatusLabel, snapshot.TrackingNumber, occurredAt,
                "Kargonomi gönderi listesinden yenilendi.");
            orderShipmentCount++;
        }

        var faultShipmentCount = 0;
        var faultTickets = await repository.GetFaultTicketsAsync(null, cancellationToken);
        foreach (var ticket in faultTickets)
        {
            foreach (var shipment in ticket.KargonomiShipments)
            {
                if (!shipment.ExternalShipmentId.HasValue ||
                    !snapshotsById.TryGetValue(shipment.ExternalShipmentId.Value, out var snapshot))
                    continue;

                shipment.ApplyUpdate(snapshot.Status, snapshot.StatusLabel, snapshot.TrackingNumber, occurredAt,
                    "Kargonomi gönderi listesinden yenilendi.");
                ApplyFaultDeliveryTransition(ticket, shipment);
                faultShipmentCount++;
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
        return new(snapshots.Count, orderShipmentCount, faultShipmentCount);
    }

    public async Task<KargonomiShipmentResponse> RefreshAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        var shipment = await repository.GetKargonomiShipmentAsync(shipmentId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kargonomi gönderisi bulunamadı.");
        if (!shipment.ExternalShipmentId.HasValue)
            throw new ConflictException("kargonomi.not_started", "Kargonomi gönderisi henüz başlatılmadı.");
        var snapshot = await client.GetShipmentAsync(shipment.ExternalShipmentId.Value, cancellationToken);
        shipment.ApplyUpdate(snapshot.Status, snapshot.StatusLabel, snapshot.TrackingNumber, timeProvider.GetUtcNow(), "Kargonomi ekranından yenilendi.");
        await repository.SaveChangesAsync(cancellationToken);
        return Map(shipment, "Öğrenci");
    }

    public async Task<string> GetBarcodeAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        var shipment = await repository.GetKargonomiShipmentAsync(shipmentId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kargonomi gönderisi bulunamadı.");
        if (!shipment.ExternalShipmentId.HasValue)
            throw new ConflictException("kargonomi.not_started", "Kargonomi gönderisi henüz başlatılmadı.");
        var barcode = await client.GetBarcodeAsync(shipment.ExternalShipmentId.Value, cancellationToken);
        shipment.SetBarcode(barcode, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return barcode;
    }

    public async Task ApplyWebhookAsync(int externalShipmentId, string? status, string? statusLabel,
        string? trackingNumber, string? description, CancellationToken cancellationToken)
    {
        var shipment = await repository.GetKargonomiShipmentByExternalIdAsync(externalShipmentId, cancellationToken);
        if (shipment is null)
        {
            await ApplyFaultWebhookAsync(externalShipmentId, status, statusLabel, trackingNumber, description, cancellationToken);
            return;
        }
        shipment.ApplyUpdate(status, statusLabel, trackingNumber, timeProvider.GetUtcNow(), description);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private void ApplyFaultDeliveryTransition(FaultTicket ticket, FaultKargonomiShipment shipment)
    {
        if (shipment.State != KargonomiShipmentState.Delivered)
            return;

        if (shipment.Direction == FaultKargonomiShipmentDirection.ToWorkshop &&
            ticket.Status == FaultStatus.WorkshopShipmentInTransit)
            ticket.MarkWorkshopReceived(SystemActorId, timeProvider.GetTurkeyNow(), "Atölye kargosu teslim edildi.");
        else if (shipment.Direction == FaultKargonomiShipmentDirection.ToCustomer &&
            ticket.Status == FaultStatus.CustomerShipmentInTransit)
            ticket.Close(SystemActorId, timeProvider.GetTurkeyNow(), "Onarılan kit müşteriye teslim edildi.");
    }

    private static KargonomiShipmentListItemResponse MapListItem(KargonomiShipmentListSnapshot item) =>
        new(item.Id, item.BuyerName, item.BuyerPhone, item.BuyerAddress, item.BuyerState, item.BuyerCity,
            item.TrackingNumber, item.Carrier, item.Status, item.StatusLabel, item.PackageCount,
            item.CreatedAt, item.UpdatedAt);

    private static KargonomiShipmentResponse Map(KargonomiShipment shipment, string studentName) =>
        new(shipment.Id, shipment.OrderId, shipment.StudentId, shipment.ExternalShipmentId, studentName, string.Empty,
            shipment.Carrier, shipment.TrackingNumber, shipment.ExternalStatus, shipment.StatusLabel, shipment.State,
            shipment.LastError, shipment.UpdatedAt, shipment.Events.Select(item => new KargonomiShipmentEventResponse(
                item.ExternalStatus, item.StatusLabel, item.State, item.TrackingNumber, item.OccurredAt, item.Description)).ToArray());
}
