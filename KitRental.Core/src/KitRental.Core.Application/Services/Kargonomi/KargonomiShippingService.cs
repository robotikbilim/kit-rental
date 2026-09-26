using KitRental.Core.Application.Abstractions;
using KitRental.Core.Application.Common;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Returns;
using KitRental.Core.Domain.Support;
using KitRental.SharedKernel;

namespace KitRental.Core.Application.Kargonomi;

public sealed class KargonomiShippingService(
    ICoreRepository repository,
    IKargonomiClient client,
    TimeProvider timeProvider)
{
    private static readonly Guid SystemActorId = new("00000000-0000-0000-0000-000000000002");
    private static readonly SemaphoreSlim[] FaultShipmentGates = Enumerable.Range(0, 64)
        .Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public async Task<FaultKargonomiShipmentResponse> StartForFaultAsync(Guid faultTicketId,
        FaultKargonomiShipmentDirection direction, CancellationToken cancellationToken)
    {
        var gate = FaultShipmentGates[(int)((uint)faultTicketId.GetHashCode() % (uint)FaultShipmentGates.Length)];
        await gate.WaitAsync(cancellationToken);
        try { return await StartFaultShipmentCoreAsync(faultTicketId, direction, cancellationToken); }
        finally { gate.Release(); }
    }

    private async Task<FaultKargonomiShipmentResponse> StartFaultShipmentCoreAsync(Guid faultTicketId,
        FaultKargonomiShipmentDirection direction, CancellationToken cancellationToken)
    {
        var ticket = await repository.GetFaultTicketAsync(faultTicketId, cancellationToken)
            ?? throw new ResourceNotFoundException("Arıza kaydı bulunamadı.");
        var existing = ticket.KargonomiShipments.Where(item => item.Direction == direction && item.State != KargonomiShipmentState.Cancelled)
            .OrderByDescending(item => item.CreatedAt).FirstOrDefault();
        if (existing?.ExternalShipmentId is not null && existing.State is not (KargonomiShipmentState.Failed or KargonomiShipmentState.Draft))
            return MapFault(existing);
        ticket.EnsureCanStartShipment(direction);
        if (string.IsNullOrWhiteSpace(ticket.ReporterName) || string.IsNullOrWhiteSpace(ticket.ReporterPhone) ||
            string.IsNullOrWhiteSpace(ticket.ReporterAddress))
            throw new ConflictException("fault_kargonomi.contact_required", "Arıza kaydındaki ad, telefon ve adres bilgileri eksiksiz olmalıdır.");
        var toWorkshop = direction == FaultKargonomiShipmentDirection.ToWorkshop;
        var destination = toWorkshop ? client.GetReturnDestination()
            : new KargonomiReturnDestination(ticket.ReporterName, ticket.ReporterPhone, ticket.ReporterAddress);
        var unit = await repository.GetProductUnitAsync(ticket.ProductUnitId, cancellationToken)
            ?? throw new ResourceNotFoundException("Arızaya bağlı fiziksel kit bulunamadı.");
        var shipment = existing ?? ticket.CreateKargonomiShipment(direction, destination.Name, destination.Phone, destination.Address, timeProvider.GetUtcNow());
        try
        {
            if (!shipment.ExternalShipmentId.HasValue)
            {
                var location = await client.ResolveLocationAsync(toWorkshop ? ticket.ReporterAddress : shipment.RecipientAddress, cancellationToken);
                var created = toWorkshop
                    ? await client.CreateReturnShipmentAsync(new KargonomiReturnShipmentRequest(
                        ticket.ReporterName, ticket.ReporterPhone, ticket.ReporterAddress, location.StateId, location.CityId,
                        $"Arızalı kit {ticket.Number} / {unit.SerialNumber}", $"{ticket.Number}-{direction}", 1), cancellationToken)
                    : await client.CreateShipmentAsync(new KargonomiCreateShipmentRequest(
                    shipment.RecipientName, shipment.RecipientPhone, shipment.RecipientAddress, location.StateId, location.CityId,
                    $"Arıza kiti {ticket.Number} / {unit.SerialNumber}", $"{ticket.Number}-{direction}", 1), cancellationToken);
                shipment.MarkCreated(created.Id, "draft", "Onay bekliyor", created.TrackingNumber, timeProvider.GetUtcNow(),
                    toWorkshop ? "HepsiJet" : "Aras Kargo");
                // Retain the provider ID before quote/confirmation so a retry uses the same shipment.
                await repository.SaveChangesAsync(cancellationToken);
            }
            var externalId = shipment.ExternalShipmentId!.Value;
            var quotes = await client.GetPriceQuotesAsync(externalId, cancellationToken);
            var carrierSlug = toWorkshop ? "hepsijet" : "aras";
            var quote = quotes.FirstOrDefault(item =>
                (item.Slug.Equals(carrierSlug, StringComparison.OrdinalIgnoreCase) || item.Name.Contains(carrierSlug, StringComparison.OrdinalIgnoreCase)) &&
                !(item.Price?.Contains("Hizmet Dışı", StringComparison.OrdinalIgnoreCase) ?? false));
            if (quote is null) throw new ConflictException("kargonomi.quote_missing", $"{(toWorkshop ? "HepsiJet" : "Aras Kargo")} için uygun fiyat teklifi bulunamadı.");
            var confirmed = await client.ConfirmShippingPriceAsync(externalId, quote.Id, cancellationToken);
            shipment.MarkCreated(confirmed.Id, confirmed.Status, confirmed.StatusLabel ?? "Hazır", confirmed.TrackingNumber,
                timeProvider.GetUtcNow(), confirmed.Carrier ?? quote.Name);
            if (toWorkshop && ticket.Status is FaultStatus.Accepted or FaultStatus.AwaitingWorkshopShipment)
                ticket.MarkWorkshopShipmentInTransit(SystemActorId, timeProvider.GetTurkeyNow(), "Arıza adresinden Robotik Bilim deposuna kurye gönderildi.");
            else if (!toWorkshop && ticket.Status != FaultStatus.CustomerShipmentInTransit)
                ticket.MarkCustomerShipmentInTransit(SystemActorId, timeProvider.GetTurkeyNow(), $"{unit.SerialNumber} seri numarası ve mevcut QR kodu korunarak arıza adresine kit gönderildi.");
            ApplyFaultDeliveryTransition(ticket, shipment);
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

    public async Task<string> GetFaultBarcodeAsync(Guid faultTicketId, Guid shipmentId, CancellationToken cancellationToken)
    {
        var ticket = await repository.GetFaultTicketAsync(faultTicketId, cancellationToken)
            ?? throw new ResourceNotFoundException("Arıza kaydı bulunamadı.");
        var shipment = ticket.KargonomiShipments.SingleOrDefault(item => item.Id == shipmentId)
            ?? throw new ResourceNotFoundException("Bu arızaya ait kargo bulunamadı.");
        if (!shipment.ExternalShipmentId.HasValue)
            throw new ConflictException("fault_kargonomi.not_created", "Henüz kargo etiketi oluşmamış, lütfen tekrar deneyin.");
        return await client.GetBarcodeAsync(shipment.ExternalShipmentId.Value, cancellationToken);
    }

    public async Task<string?> StartForReturnAsync(KitReturnRequest request, ProductUnit unit,
        CancellationToken cancellationToken)
    {
        if (request.Status != KitReturnStatus.Requested ||
            (!string.IsNullOrWhiteSpace(request.Carrier) && !string.IsNullOrWhiteSpace(request.TrackingNumber)))
            return null;
        if (string.IsNullOrWhiteSpace(request.RequesterName) || string.IsNullOrWhiteSpace(request.RequesterPhone) ||
            string.IsNullOrWhiteSpace(request.ReturnAddress))
            throw new ConflictException("kargonomi.return_details_required",
                "Kurye çağırmak için ad, telefon ve adres bilgileri zorunludur.");

        var location = await client.ResolveLocationAsync(request.ReturnAddress, cancellationToken);
        var created = await client.CreateReturnShipmentAsync(new KargonomiReturnShipmentRequest(
            request.RequesterName, request.RequesterPhone, request.ReturnAddress,
            location.StateId, location.CityId, $"İade kiti {unit.SerialNumber}", unit.SerialNumber, 1), cancellationToken);
        var quotes = await client.GetPriceQuotesAsync(created.Id, cancellationToken);
        var hepsiJet = quotes.FirstOrDefault(item =>
            (item.Slug.Equals("hepsijet", StringComparison.OrdinalIgnoreCase) ||
             item.Name.Contains("HepsiJet", StringComparison.OrdinalIgnoreCase)) &&
            !(item.Price?.Contains("Hizmet Dışı", StringComparison.OrdinalIgnoreCase) ?? false));
        if (hepsiJet is null)
            throw new ConflictException("kargonomi.hepsijet_quote_missing",
                "İade için HepsiJet fiyat teklifi bulunamadı veya bu bölge HepsiJet'e kapalı.");

        var confirmed = await client.ConfirmShippingPriceAsync(created.Id, hepsiJet.Id, cancellationToken);
        // Kargonomi barkod ve takip kodunu onay yanıtında hemen üretmeyebilir.
        // Public form, bu çağrı tamamlanana kadar loading ekranında kaldığı için
        // en az bir müşteri-facing kod oluşana kadar sağlayıcıyı kısa aralıklarla sorgula.
        for (var attempt = 0; attempt < 30 &&
             string.IsNullOrWhiteSpace(confirmed.Barcode) &&
             string.IsNullOrWhiteSpace(confirmed.TrackingNumber); attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            confirmed = await client.GetShipmentAsync(created.Id, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(confirmed.Barcode) &&
            string.IsNullOrWhiteSpace(confirmed.TrackingNumber))
            throw new ConflictException("kargonomi.return_code_pending",
                "Kargo barkodu veya takip kodu henüz oluşmadı. Lütfen birkaç dakika sonra tekrar deneyin.");

        var shippedAt = timeProvider.GetUtcNow();
        request.MarkKargonomiShipment("HepsiJet", confirmed.TrackingNumber, shippedAt);
        request.LinkExternalShipment(confirmed.Id, confirmed.Carrier, confirmed.TrackingNumber, confirmed.Barcode,
            confirmed.Status, confirmed.StatusLabel, confirmed.UpdatedAt ?? shippedAt);
        return confirmed.Barcode;
    }

    public async Task<string> GetReturnBarcodeAsync(Guid returnId, CancellationToken cancellationToken)
    {
        var request = await repository.GetKitReturnRequestAsync(returnId, cancellationToken)
            ?? throw new ResourceNotFoundException("İade kaydı bulunamadı.");
        if (!request.ExternalShipmentId.HasValue)
            throw new ConflictException("kargonomi.return_not_linked", "İade kaydı Kargonomi gönderisi ile eşleşmedi.");

        return await client.GetBarcodeAsync(request.ExternalShipmentId.Value, cancellationToken);
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

    internal static FaultKargonomiShipmentResponse MapFault(FaultKargonomiShipment shipment) =>
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
        string? trackingNumber, string? carrier, string? barcode, string? description,
        CancellationToken cancellationToken)
    {
        var shipment = await repository.GetKargonomiShipmentByExternalIdAsync(externalShipmentId, cancellationToken);
        if (shipment is not null)
        {
            shipment.ApplyUpdate(status, statusLabel, trackingNumber, timeProvider.GetUtcNow(), description);
            await repository.SaveChangesAsync(cancellationToken);
            return;
        }

        var returnRequest = await repository.GetKitReturnRequestByExternalShipmentIdAsync(
            externalShipmentId, cancellationToken);
        if (returnRequest is not null)
        {
            returnRequest.ApplyKargonomiUpdate(carrier, trackingNumber, barcode, status, statusLabel,
                timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(cancellationToken);
            return;
        }

        await ApplyFaultWebhookAsync(externalShipmentId, status, statusLabel, trackingNumber, description,
            cancellationToken);
    }

    private void ApplyFaultDeliveryTransition(FaultTicket ticket, FaultKargonomiShipment shipment)
    {
        if (shipment.State != KargonomiShipmentState.Delivered)
            return;

        if (shipment.Direction == FaultKargonomiShipmentDirection.ToWorkshop &&
            ticket.Status == FaultStatus.WorkshopShipmentInTransit)
            ticket.MarkWorkshopReceived(SystemActorId, timeProvider.GetTurkeyNow(), "Atölye kargosu teslim edildi.");
        // Either leg may finish first. Do not close the fault while collection is still outstanding.
        if (ticket.Status == FaultStatus.CustomerShipmentInTransit &&
            ticket.KargonomiShipments.Any(item => item.Direction == FaultKargonomiShipmentDirection.ToCustomer && item.State == KargonomiShipmentState.Delivered) &&
            (ticket.KargonomiShipments.Any(item => item.Direction == FaultKargonomiShipmentDirection.ToWorkshop && item.State == KargonomiShipmentState.Delivered) ||
             ticket.History.Any(item => item.Current == FaultStatus.WorkshopReceived)))
            ticket.Close(SystemActorId, timeProvider.GetTurkeyNow(), "Yeni kit veliye teslim edildi ve arızalı kit depoya alındı.");
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
