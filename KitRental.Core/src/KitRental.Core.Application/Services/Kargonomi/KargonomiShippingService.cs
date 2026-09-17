using KitRental.Core.Application.Abstractions;
using KitRental.Core.Application.Common;
using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Orders;

namespace KitRental.Core.Application.Kargonomi;

public sealed class KargonomiShippingService(
    ICoreRepository repository,
    IKargonomiClient client,
    TimeProvider timeProvider)
{
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
            try
            {
                var existing = await repository.GetKargonomiShipmentAsync(orderId, student.Id, cancellationToken);
                if (existing is not null && existing.ExternalShipmentId.HasValue)
                {
                    attempts.Add(new(student.Id, student.FullName, true, "Kargo zaten başlatılmış.", Map(existing, student.FullName)));
                    continue;
                }
                if (!student.HasAddress)
                    throw new ConflictException("kargonomi.address_required", "Öğrenci adresi tamamlanmadan kargo başlatılamaz.");

                var shipment = existing ?? KargonomiShipment.Create(Guid.NewGuid(), orderId, student.Id,
                    timeProvider.GetUtcNow());
                if (existing is null)
                    await repository.AddKargonomiShipmentAsync(shipment, cancellationToken);

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
                var failed = await repository.GetKargonomiShipmentAsync(orderId, student.Id, cancellationToken);
                if (failed is null)
                {
                    failed = KargonomiShipment.Create(Guid.NewGuid(), orderId, student.Id, timeProvider.GetUtcNow());
                    await repository.AddKargonomiShipmentAsync(failed, cancellationToken);
                }
                failed.MarkFailed(exception.Message, timeProvider.GetUtcNow());
                await repository.SaveChangesAsync(cancellationToken);
                attempts.Add(new(student.Id, student.FullName, false, exception.Message, Map(failed, student.FullName)));
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
    {
        var orders = (await repository.GetOrdersAsync(null, cancellationToken)).ToDictionary(item => item.Id);
        var students = (await repository.GetRentalCohortsAsync(null, cancellationToken)).SelectMany(item => item.Students)
            .Where(item => !item.IsDeleted).ToDictionary(item => item.Id);
        return (await repository.GetKargonomiShipmentsAsync(null, cancellationToken)).Select(item =>
        {
            students.TryGetValue(item.StudentId, out var student);
            orders.TryGetValue(item.OrderId, out var order);
            return new KargonomiShipmentListItemResponse(item.Id, item.OrderId, order?.OrderNumber ?? "-",
                item.StudentId, student?.FullName ?? "Öğrenci", item.TrackingNumber, item.Carrier, item.StatusLabel,
                item.State, item.LastError, item.UpdatedAt);
        }).ToArray();
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
        var shipment = await repository.GetKargonomiShipmentByExternalIdAsync(externalShipmentId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kargonomi gönderisi eşleşmedi.");
        shipment.ApplyUpdate(status, statusLabel, trackingNumber, timeProvider.GetUtcNow(), description);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private static KargonomiShipmentResponse Map(KargonomiShipment shipment, string studentName) =>
        new(shipment.Id, shipment.OrderId, shipment.StudentId, shipment.ExternalShipmentId, studentName, string.Empty,
            shipment.Carrier, shipment.TrackingNumber, shipment.ExternalStatus, shipment.StatusLabel, shipment.State,
            shipment.LastError, shipment.UpdatedAt, shipment.Events.Select(item => new KargonomiShipmentEventResponse(
                item.ExternalStatus, item.StatusLabel, item.State, item.TrackingNumber, item.OccurredAt, item.Description)).ToArray());
}
