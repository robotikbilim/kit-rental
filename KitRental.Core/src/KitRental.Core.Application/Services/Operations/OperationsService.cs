using KitRental.Core.Application.Abstractions;
using KitRental.Core.Application.Common;
using KitRental.Core.Application.Inventory;
using KitRental.Core.Domain.Auditing;
using KitRental.Core.Domain.Customers;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Rentals;
using KitRental.Core.Domain.Returns;
using KitRental.Core.Domain.Support;
using KitRental.Core.Application.Kargonomi;
using KitRental.SharedKernel;

namespace KitRental.Core.Application.Operations;

public sealed record AddressCommand(string Title, string ContactName, string Phone, string Line1, string PostalCode);
public sealed record CreateCustomerCommand(string Name, string Email, AddressCommand Address, Guid ActorId,
    IReadOnlyCollection<Guid>? AllowedProductModelIds = null);
public sealed record UpdateCustomerCommand(Guid CustomerId, string Name, string Email, bool IsActive, Guid ActorId,
    IReadOnlyCollection<Guid>? AllowedProductModelIds = null);
public sealed record CustomerAddressCommand(Guid CustomerId, Guid? AddressId, AddressCommand Address, Guid ActorId);
public sealed record OrderLineCommand(Guid ProductModelId, int Quantity);
public sealed record CreateOrderCommand(Guid CustomerId, Guid AddressId, DateOnly StartDate, DateOnly EndDate, IReadOnlyCollection<OrderLineCommand> Lines, Guid ActorId);
public sealed record CreateStudentAddressOrderStudentCommand(string FullName, string GuardianPhone);
public sealed record CreateStudentAddressOrderCommand(Guid CustomerId, Guid ProductModelId, DateOnly StartDate,
    DateOnly EndDate, IReadOnlyCollection<CreateStudentAddressOrderStudentCommand> Students, Guid ActorId);
public sealed record UpdateOrderRentalPeriodCommand(Guid OrderId, string PeriodName, DateOnly StartDate,
    DateOnly EndDate, Guid ActorId);
public sealed record UpdateOrderStudentCommand(Guid OrderId, Guid StudentId, string FullName, string GuardianPhone,
    Guid ActorId);
public sealed record AddOrderStudentCommand(Guid OrderId, string FullName, string GuardianPhone, Guid ActorId);
public sealed record CreatePurchaseOrderCommand(Guid CustomerId, Guid AddressId,
    IReadOnlyCollection<OrderLineCommand> Lines, Guid ActorId);
public sealed record OpenFaultCommand(Guid CustomerId, Guid OrderId, Guid AssignmentId, Guid ProductUnitId,
    string Category, FaultSeverity Severity, string Description, Guid ActorId, string? ReporterName = null,
    string? ReporterPhone = null, string? ReporterAddress = null, double? Latitude = null, double? Longitude = null,
    FaultOrigin Origin = FaultOrigin.Internal, string? AttachmentUrl = null);
public sealed record PublicFaultKitResponse(string QrCode, Guid ProductUnitId, string KitName, string SerialNumber);
public sealed record PublicKitDeliveryContextResponse(string? RecipientName, string? RecipientPhone,
    string? AddressLine, double? Latitude, double? Longitude);
public sealed record PublicFaultContextResponse(Guid? FaultId, string? ReporterName, string? ReporterPhone,
    string? ReporterAddress, string? Description,
    double? Latitude, double? Longitude, string? AttachmentUrl = null);
public sealed record OpenPublicFaultCommand(string QrCode, string ReporterName, string ReporterPhone,
    string ReporterAddress, string Description,
    double? Latitude, double? Longitude, string? AttachmentUrl = null);
public sealed record CreatePublicKitDeliveryCommand(string QrCode, string RecipientName,
    string RecipientPhone, string AddressLine,
    double? Latitude, double? Longitude);
public sealed record FaultGuideEntryResponse(Guid Id, string Title, string Problem, string Solution,
    int DisplayOrder, bool IsActive, DateTimeOffset UpdatedAt, Guid? ProductModelId = null,
    string? ProductModelName = null);
public sealed record SaveFaultGuideEntryCommand(Guid? Id, string Title, string Problem, string Solution,
    int DisplayOrder, bool IsActive, Guid ActorId, Guid? ProductModelId = null);
public sealed record InspectionItemCommand(string Name, bool IsPresent, bool IsDamaged, string Note);
public sealed record CompleteInspectionCommand(Guid OrderId, Guid ProductUnitId, IReadOnlyCollection<InspectionItemCommand> Items, decimal DamageCharge, ProductUnitStatus Outcome, Guid ActorId);
public sealed record FaultPageQuery(string? Query, FaultStatus? Status, FaultSeverity? Severity,
    DateOnly? OpenedFrom, DateOnly? OpenedTo, int Page = 1, int PageSize = 20, Guid? CustomerId = null, Guid? OrderId = null, string? Stage = null);
public sealed record FaultListItemResponse(Guid Id, string Number, Guid CustomerId, string CustomerName,
    string ReporterName, string ReporterPhone, string ReporterAddress, string Category, FaultSeverity Severity, string Description,
    FaultStatus Status, DateTimeOffset OpenedAt, FaultApprovalStatus ApprovalStatus, FaultOrigin Origin,
    string? AttachmentUrl = null, Guid OrderId = default, string? OrderNumber = null,
    Guid ProductUnitId = default, string? SerialNumber = null,
    IReadOnlyCollection<FaultKargonomiShipmentResponse>? Shipments = null);
public sealed record FaultKitLabelResponse(Guid Id, string KitName, string KitSku, string SerialNumber,
    string QrCode, string RecipientName, string RecipientPhone, string RecipientAddress);
public sealed record FaultPageResponse(int Page, int PageSize, int TotalCount, int TotalPages,
    IReadOnlyCollection<FaultListItemResponse> Items);
public sealed record OrderKitResponse(Guid ProductUnitId, Guid AssignmentId, Guid ProductModelId,
    string SerialNumber, ProductUnitStatus Status);
public sealed record OrderKitPreparationResponse(Guid OrderId, int CreatedCount, int ReusedCount,
    IReadOnlyCollection<OrderKitResponse> Kits);
public sealed record OrderKitLineCommand(Guid ProductModelId, int Quantity);
public sealed record OrderDetailLineResponse(Guid Id, Guid ProductModelId, string ProductName, string ProductSku,
    int Quantity, int CreatedKitCount);
public sealed record OrderDetailKitResponse(Guid Id, Guid OrderLineId, Guid ProductModelId, string ProductName,
    string ProductSku, string SerialNumber, string QrCode, ProductUnitStatus Status);
public sealed record OrderDetailStudentResponse(Guid Id, string FullName, string GuardianPhone, string AddressLine,
    bool HasAddress, string PublicAddressToken, DateTimeOffset? AddressSubmittedAt, Guid ProductModelId = default,
    string ProductName = "", string ProductSku = "", bool IsDelivered = false, bool HasKitAssignment = false,
    string AssignedKitSerialNumber = "", string AssignedKitQrCode = "", ProductUnitStatus? AssignedKitStatus = null,
    Guid? AssignedKitId = null);
public sealed record OrderDetailResponse(Guid Id, string OrderNumber, Guid CustomerId, string CustomerName, OrderType Type,
    RentalOrderStatus Status, DateOnly? StartDate, DateOnly? EndDate, DateTimeOffset CreatedAt, Guid? RentalCohortId,
    string? RentalPeriodName,
    IReadOnlyCollection<OrderDetailLineResponse> Lines, IReadOnlyCollection<OrderDetailKitResponse> Kits,
    IReadOnlyCollection<OrderDetailStudentResponse> Students,
    IReadOnlyCollection<KargonomiShipmentResponse> KargonomiShipments = default!);
public sealed record PublicStudentAddressContextResponse(string StudentName, string GuardianPhone, string CustomerName,
    string OrderNumber, string ProductName, string? AddressLine, double? Latitude, double? Longitude);
public sealed record SavePublicStudentAddressCommand(string Token, string AddressLine, double? Latitude,
    double? Longitude);
public sealed record ReturnListItemResponse(Guid Id, string CustomerName, int Status, string? Carrier,
    string? TrackingNumber, DateTimeOffset CreatedAt, int KitCount, string? RequesterName = null,
    string? RequesterPhone = null, string? ReturnAddress = null,
    double? Latitude = null, double? Longitude = null,
    KitReturnDeliveryMethod DeliveryMethod = KitReturnDeliveryMethod.PickupFromAddress);
public sealed record ReturnTableItemResponse(Guid ProductUnitId, Guid AssignmentId, Guid? ReturnId,
    Guid? StudentId, string CustomerName, string StudentName, string GuardianPhone,
    string ProductModelName, string ProductModelSku, string SerialNumber, string OrderNumber,
    DateOnly StartDate, DateOnly EndDate, int UnitStatus, int AssignmentStatus, int ReturnStatus,
    string ReturnStateKey, string ReturnState, string? Carrier, string? TrackingNumber,
    int? ExternalShipmentId, string? KargonomiStatus, string? KargonomiStatusLabel, string? KargonomiBarcode,
    DateTimeOffset? ReturnCreatedAt, DateTimeOffset? ShippedAt, DateTimeOffset? ReceivedAt,
    string? AddressLine, string? PublicAddressToken, string? RequesterName, string? RequesterPhone,
    int DeliveryMethod, Guid OrderId = default);

public sealed class OperationsService(
    ICoreRepository repository,
    TimeProvider timeProvider,
    ProductUnitStockConsumptionPlanner stockConsumptionPlanner)
{
    private static readonly Guid PublicActorId = new("00000000-0000-0000-0000-000000000001");

    public async Task<Customer> CreateCustomerAsync(CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        var customer = Customer.Create(Guid.NewGuid(), command.Name, command.Email);
        var allowedProductModelIds = await ValidateAllowedProductModelsAsync(command.AllowedProductModelIds, cancellationToken);
        customer.SetAllowedProductModels(allowedProductModelIds);
        customer.AddAddress(
            command.Address.Title, command.Address.ContactName, command.Address.Phone, command.Address.Line1,
            command.Address.PostalCode);
        try
        {
            await repository.AddCustomerAsync(customer, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflictException("customer.email_not_unique", exception.Message);
        }
        await AuditAsync(command.ActorId, nameof(Customer), customer.Id, "Created", null, customer.Name, cancellationToken);
        return customer;
    }

    public Task<IReadOnlyCollection<Customer>> GetCustomersAsync(CancellationToken cancellationToken) =>
        repository.GetCustomersAsync(cancellationToken);

    public Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken) =>
        repository.GetCustomerAsync(customerId, cancellationToken);

    public async Task<Customer> UpdateCustomerAsync(UpdateCustomerCommand command, CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(command.CustomerId, cancellationToken)
            ?? throw new ResourceNotFoundException("Müşteri bulunamadı.");
        var duplicate = await repository.FindCustomerByEmailAsync(command.Email, cancellationToken);
        if (duplicate is not null && duplicate.Id != customer.Id)
            throw new ConflictException("customer.email_not_unique", "Müşteri e-posta adresi benzersiz olmalıdır.");
        var previousAllowedProductModelIds = string.Join(',', customer.AllowedProductModelIds.Order());
        var previous = $"{customer.Name}|{customer.Email}|{customer.IsActive}|{previousAllowedProductModelIds}";
        customer.Update(command.Name, command.Email);
        customer.SetActive(command.IsActive);
        if (command.AllowedProductModelIds is not null)
        {
            var allowedProductModelIds = await ValidateAllowedProductModelsAsync(command.AllowedProductModelIds, cancellationToken);
            customer.SetAllowedProductModels(allowedProductModelIds);
        }
        await repository.SaveChangesAsync(cancellationToken);
        var currentAllowedProductModelIds = string.Join(',', customer.AllowedProductModelIds.Order());
        await AuditAsync(command.ActorId, nameof(Customer), customer.Id, "Updated", previous,
            $"{customer.Name}|{customer.Email}|{customer.IsActive}|{currentAllowedProductModelIds}", cancellationToken);
        return customer;
    }

    private async Task<IReadOnlyCollection<Guid>> ValidateAllowedProductModelsAsync(
        IReadOnlyCollection<Guid>? productModelIds,
        CancellationToken cancellationToken)
    {
        var distinctIds = productModelIds?.Where(id => id != Guid.Empty).Distinct().ToArray() ?? [];
        foreach (var productModelId in distinctIds)
        {
            if (await repository.GetProductModelAsync(productModelId, cancellationToken) is null)
                throw new ResourceNotFoundException($"{productModelId} eğitim kiti bulunamadı.");
        }

        return distinctIds;
    }

    public async Task<Customer> SetCustomerActiveAsync(Guid customerId, bool isActive, Guid actorId,
        CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(customerId, cancellationToken)
            ?? throw new ResourceNotFoundException("Müşteri bulunamadı.");
        customer.SetActive(isActive);
        await repository.SaveChangesAsync(cancellationToken);
        await AuditAsync(actorId, nameof(Customer), customer.Id, isActive ? "Activated" : "Deactivated",
            (!isActive).ToString(), isActive.ToString(), cancellationToken);
        return customer;
    }

    public async Task<Address> AddCustomerAddressAsync(CustomerAddressCommand command, CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(command.CustomerId, cancellationToken)
            ?? throw new ResourceNotFoundException("Müşteri bulunamadı.");
        var address = customer.AddAddress(command.Address.Title, command.Address.ContactName, command.Address.Phone,
            command.Address.Line1, command.Address.PostalCode);
        await repository.SaveChangesAsync(cancellationToken);
        await AuditAsync(command.ActorId, nameof(Customer), customer.Id, "AddressAdded", null, address.Title, cancellationToken);
        return address;
    }

    public async Task<Address> UpdateCustomerAddressAsync(CustomerAddressCommand command, CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(command.CustomerId, cancellationToken)
            ?? throw new ResourceNotFoundException("Müşteri bulunamadı.");
        var address = customer.UpdateAddress(command.AddressId ?? Guid.Empty, command.Address.Title,
            command.Address.ContactName, command.Address.Phone, command.Address.Line1, command.Address.PostalCode);
        await repository.SaveChangesAsync(cancellationToken);
        await AuditAsync(command.ActorId, nameof(Customer), customer.Id, "AddressUpdated", null, address.Title, cancellationToken);
        return address;
    }

    public async Task RemoveCustomerAddressAsync(Guid customerId, Guid addressId, Guid actorId,
        CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(customerId, cancellationToken)
            ?? throw new ResourceNotFoundException("Müşteri bulunamadı.");
        customer.RemoveAddress(addressId);
        await repository.SaveChangesAsync(cancellationToken);
        await AuditAsync(actorId, nameof(Customer), customer.Id, "AddressRemoved", addressId.ToString(), null, cancellationToken);
    }

    public async Task<RentalOrder> CreateOrderAsync(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(command.CustomerId, cancellationToken)
            ?? throw new ResourceNotFoundException("Müşteri bulunamadı.");
        if (command.Lines.Count == 0)
            throw new ConflictException("order.lines_required", "Siparişte en az bir ürün satırı bulunmalıdır.");

        foreach (var line in command.Lines)
        {
            if (await repository.GetProductModelAsync(line.ProductModelId, cancellationToken) is null)
                throw new ResourceNotFoundException($"{line.ProductModelId} ürün modeli bulunamadı.");
        }

        var now = timeProvider.GetTurkeyNow();
        var order = RentalOrder.Create(
            Guid.NewGuid(),
            $"RR-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..20],
            customer.Id,
            new RentalPeriod(command.StartDate, command.EndDate),
            customer.SnapshotAddress(command.AddressId),
            now);
        foreach (var line in command.Lines)
            order.AddLine(line.ProductModelId, line.Quantity);
        order.Submit(command.ActorId, now);
        await repository.AddOrderAsync(order, cancellationToken);
        await AuditAsync(command.ActorId, nameof(RentalOrder), order.Id, "Submitted", null, order.Status.ToString(), cancellationToken);
        return order;
    }

    public async Task<RentalOrder> CreateStudentAddressOrderAsync(CreateStudentAddressOrderCommand command,
        CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(command.CustomerId, cancellationToken)
            ?? throw new ResourceNotFoundException("Müşteri bulunamadı.");
        var productModel = await repository.GetProductModelAsync(command.ProductModelId, cancellationToken)
            ?? throw new ResourceNotFoundException("Eğitim kiti bulunamadı.");
        if (!customer.CanUseProductModel(productModel.Id))
            throw new ForbiddenException("Bu eğitim kiti müşterinin kullanımına açık değil.");
        var address = customer.Addresses.FirstOrDefault()
            ?? throw new ConflictException("customer.address_required",
                "Sipariş oluşturmak için müşterinin kayıtlı bir adresi bulunmalıdır.");
        var students = command.Students
            .Where(student => !string.IsNullOrWhiteSpace(student.FullName) ||
                !string.IsNullOrWhiteSpace(student.GuardianPhone))
            .ToArray();
        if (students.Length == 0)
            throw new ConflictException("order.students_required", "Sipariş için en az bir öğrenci girilmelidir.");

        var now = timeProvider.GetTurkeyNow();
        var order = RentalOrder.Create(
            Guid.NewGuid(),
            $"RR-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..20],
            customer.Id,
            new RentalPeriod(command.StartDate, command.EndDate),
            customer.SnapshotAddress(address.Id),
            now);
        order.AddLine(productModel.Id, students.Length);
        order.Submit(command.ActorId, now);

        var cohort = RentalCohort.Create(Guid.NewGuid(), customer.Id, order.OrderNumber, command.StartDate,
            command.EndDate, now);
        foreach (var student in students)
            cohort.AddStudent(student.FullName, student.GuardianPhone, string.Empty, productModel.Id);
        cohort.LinkActiveStudentsToOrder(order.Id);

        await repository.AddOrderAsync(order, cancellationToken);
        await repository.AddRentalCohortAsync(cohort, cancellationToken);
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), command.ActorId, nameof(RentalOrder),
            order.Id, "SubmittedWithStudentAddressCollection", null, $"{students.Length} öğrenci", now),
            cancellationToken);
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), command.ActorId, nameof(RentalCohort),
            cohort.Id, "CreatedForOrderAddressCollection", null, order.OrderNumber, now), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return order;
    }

    public async Task<RentalOrder> CreatePurchaseOrderAsync(CreatePurchaseOrderCommand command,
        CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(command.CustomerId, cancellationToken)
            ?? throw new ResourceNotFoundException("Müşteri bulunamadı.");
        await ValidateOrderLinesAsync(command.Lines, cancellationToken);
        var now = timeProvider.GetTurkeyNow();
        var order = RentalOrder.CreatePurchase(Guid.NewGuid(),
            $"SO-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..20], customer.Id,
            customer.SnapshotAddress(command.AddressId), now);
        foreach (var line in command.Lines)
            order.AddLine(line.ProductModelId, line.Quantity);
        order.Submit(command.ActorId, now);
        order.Approve(command.ActorId, now);
        await repository.AddOrderAsync(order, cancellationToken);
        await AuditAsync(command.ActorId, nameof(RentalOrder), order.Id, "PurchaseOrderCreated", null,
            $"{order.Type}|{order.Status}", cancellationToken);
        return order;
    }

    public Task<IReadOnlyCollection<RentalOrder>> GetOrdersAsync(Guid? customerId, CancellationToken cancellationToken) =>
        repository.GetOrdersAsync(customerId, cancellationToken);

    public async Task<OrderDetailResponse> GetOrderDetailAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(orderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Sipariş bulunamadı.");
        var customer = await repository.GetCustomerAsync(order.CustomerId, cancellationToken);
        var models = (await repository.GetProductModelsAsync(cancellationToken)).ToDictionary(item => item.Id);
        var assignments = await repository.GetAssignmentsForOrderAsync(order.Id, cancellationToken);
        var deliveredAssignmentIds = (await repository.GetKitLocationEventsAsync(cancellationToken))
            .Where(item => item.OrderId == order.Id && item.Source == KitLocationEventSource.DeliveryReceipt && item.AssignmentId.HasValue)
            .Select(item => item.AssignmentId!.Value)
            .ToHashSet();
        var assignedUnits = order.Type == OrderType.Rental
            ? assignments.Where(item => item.Status != RentalAssignmentStatus.Cancelled)
                .Select(item => new { item.OrderLineId, item.ProductUnitId }).ToArray()
            : order.ProductUnits.Select(item => new { item.OrderLineId, item.ProductUnitId }).ToArray();
        var assignmentCounts = assignedUnits.GroupBy(item => item.OrderLineId)
            .ToDictionary(group => group.Key, group => group.Count());
        var lines = order.Lines.Select(line => new OrderDetailLineResponse(line.Id, line.ProductModelId,
            models.TryGetValue(line.ProductModelId, out var model) ? model.Name : "Eğitim kiti",
            models.TryGetValue(line.ProductModelId, out model) ? model.Sku : "-", line.Quantity,
            assignmentCounts.GetValueOrDefault(line.Id))).ToArray();
        var kits = new List<OrderDetailKitResponse>();
        foreach (var assignment in assignedUnits)
        {
            var unit = await repository.GetProductUnitAsync(assignment.ProductUnitId, cancellationToken);
            if (unit is null) continue;
            models.TryGetValue(unit.ProductModelId, out var model);
            kits.Add(new OrderDetailKitResponse(unit.Id, assignment.OrderLineId, unit.ProductModelId,
                model?.Name ?? "Eğitim kiti", model?.Sku ?? "-", unit.SerialNumber, unit.QrCode, unit.Status));
        }
        var cohort = (await repository.GetRentalCohortsAsync(order.CustomerId, cancellationToken))
            .FirstOrDefault(cohort => cohort.Students.Any(student => student.OrderId == order.Id));
        var students = cohort?.Students
            .Where(student => !student.IsDeleted && student.OrderId == order.Id)
            .OrderBy(student => student.FullName)
            .Select(student =>
            {
                models.TryGetValue(student.ProductModelId, out var productModel);
                return new OrderDetailStudentResponse(student.Id, student.FullName, student.GuardianPhone,
                    student.AddressLine, student.HasAddress, student.PublicAddressToken, student.AddressSubmittedAt,
                    student.ProductModelId, productModel?.Name ?? "Eğitim kiti", productModel?.Sku ?? "-",
                    student.AssignmentId is { } assignmentId && deliveredAssignmentIds.Contains(assignmentId),
                    student.HasKitAssignment,
                    student.ProductUnitId is { } unitId ? kits.FirstOrDefault(item => item.Id == unitId)?.SerialNumber ?? "" : "",
                    student.ProductUnitId is { } qrUnitId ? kits.FirstOrDefault(item => item.Id == qrUnitId)?.QrCode ?? "" : "",
                    student.ProductUnitId is { } statusUnitId ? kits.FirstOrDefault(item => item.Id == statusUnitId)?.Status : null,
                    student.ProductUnitId);
            })
            .ToArray() ?? [];
        var kargonomiShipments = (await repository.GetKargonomiShipmentsAsync(order.Id, cancellationToken))
            .Select(shipment => new KargonomiShipmentResponse(shipment.Id, shipment.OrderId, shipment.StudentId,
                shipment.ExternalShipmentId,
                students.FirstOrDefault(student => student.Id == shipment.StudentId)?.FullName ?? "Öğrenci",
                students.FirstOrDefault(student => student.Id == shipment.StudentId)?.AddressLine ?? string.Empty,
                shipment.Carrier, shipment.TrackingNumber, shipment.ExternalStatus, shipment.StatusLabel,
                shipment.State, shipment.LastError, shipment.UpdatedAt,
                shipment.Events.Select(item => new KargonomiShipmentEventResponse(item.ExternalStatus,
                    item.StatusLabel, item.State, item.TrackingNumber, item.OccurredAt, item.Description)).ToArray()))
            .ToArray();
        return new OrderDetailResponse(order.Id, order.OrderNumber, order.CustomerId, customer?.Name ?? "Müşteri", order.Type,
            order.Status, order.Period?.StartDate, order.Period?.EndDate, order.CreatedAt, cohort?.Id,
            cohort?.Name, lines,
            kits.OrderBy(item => item.ProductName).ThenBy(item => item.SerialNumber).ToArray(), students,
            kargonomiShipments);
    }

    public async Task<OrderDetailResponse> UpdateOrderRentalPeriodAsync(
        UpdateOrderRentalPeriodCommand command, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(command.OrderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Sipariş bulunamadı.");
        if (order.Type != OrderType.Rental)
            throw new ConflictException("order.period_not_editable", "Satın alma siparişlerinin dönem bilgileri düzenlenemez.");

        var cohorts = await repository.GetRentalCohortsAsync(order.CustomerId, cancellationToken);
        var cohort = cohorts.FirstOrDefault(item => item.Students.Any(student => student.OrderId == order.Id));
        if (cohort is null)
            throw new ConflictException("order.period_not_editable", "Bu sipariş için düzenlenebilir bir dönem kaydı bulunamadı.");

        var previousValue = $"{cohort.Name}|{cohort.StartDate:O}/{cohort.EndDate:O}";
        order.UpdateRentalPeriod(command.StartDate, command.EndDate);
        cohort.Update(command.PeriodName, command.StartDate, command.EndDate);
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), command.ActorId, nameof(RentalOrder),
            order.Id, "RentalPeriodUpdated", previousValue,
            $"{cohort.Name}|{command.StartDate:O}/{command.EndDate:O}", timeProvider.GetTurkeyNow()), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await GetOrderDetailAsync(order.Id, cancellationToken);
    }

    public async Task<OrderDetailResponse> UpdateOrderStudentAsync(
        UpdateOrderStudentCommand command, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(command.OrderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Sipariş bulunamadı.");
        if (order.Type != OrderType.Rental)
            throw new ConflictException("order.student_not_editable", "Öğrenci yalnızca kiralama siparişinde güncellenebilir.");

        var cohort = (await repository.GetRentalCohortsAsync(order.CustomerId, cancellationToken))
            .FirstOrDefault(item => item.Students.Any(student => student.Id == command.StudentId &&
                student.OrderId == command.OrderId))
            ?? throw new ResourceNotFoundException("Sipariş öğrencisi bulunamadı.");
        var student = cohort.Students.FirstOrDefault(item => item.Id == command.StudentId &&
            !item.IsDeleted && item.OrderId == command.OrderId)
            ?? throw new ResourceNotFoundException("Sipariş öğrencisi bulunamadı.");

        var previousValue = $"{student.FullName}|{student.GuardianPhone}";
        cohort.UpdateStudent(student.Id, command.FullName, command.GuardianPhone, student.AddressLine,
            student.ProductModelId);
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), command.ActorId, nameof(RentalCohort),
            cohort.Id, "OrderStudentUpdated", previousValue,
            $"{student.FullName}|{student.GuardianPhone}", timeProvider.GetTurkeyNow()), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await GetOrderDetailAsync(order.Id, cancellationToken);
    }

    public async Task<OrderDetailResponse> AddOrderStudentAsync(
        AddOrderStudentCommand command, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(command.OrderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Sipariş bulunamadı.");
        if (order.Type != OrderType.Rental)
            throw new ConflictException("order.student_add_not_allowed", "Öğrenci yalnızca kiralama siparişine eklenebilir.");

        var cohort = (await repository.GetRentalCohortsAsync(order.CustomerId, cancellationToken))
            .FirstOrDefault(item => item.Students.Any(student => student.OrderId == command.OrderId))
            ?? throw new ConflictException("order.student_add_not_allowed", "Bu sipariş için öğrenci listesi bulunamadı.");
        var productModelId = order.Lines.FirstOrDefault()?.ProductModelId
            ?? throw new ConflictException("order.student_add_not_allowed", "Siparişte eklenecek öğrenci için kit satırı bulunamadı.");

        order.AddOneKitRequirement(productModelId);
        var student = cohort.AddStudent(command.FullName, command.GuardianPhone, string.Empty, productModelId);
        student.LinkOrder(order.Id);
        var now = timeProvider.GetTurkeyNow();
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), command.ActorId, nameof(RentalOrder),
            order.Id, "OrderStudentAdded", null, $"{student.FullName}|{student.GuardianPhone}", now), cancellationToken);
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), command.ActorId, nameof(RentalCohort),
            cohort.Id, "StudentAddedToOrder", null, student.FullName, now), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await GetOrderDetailAsync(order.Id, cancellationToken);
    }

    public async Task RemoveStudentFromOrderAsync(Guid orderId, Guid studentId, Guid actorId,
        CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(orderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Sipariş bulunamadı.");
        if (order.Type != OrderType.Rental)
            throw new ConflictException("order.student_remove_not_allowed", "Öğrenci yalnızca kiralama siparişinden silinebilir.");
        var cohort = (await repository.GetRentalCohortsAsync(order.CustomerId, cancellationToken))
            .FirstOrDefault(item => item.Students.Any(student => student.Id == studentId && student.OrderId == orderId));
        var student = cohort?.Students.FirstOrDefault(item => item.Id == studentId && !item.IsDeleted && item.OrderId == orderId)
            ?? throw new ResourceNotFoundException("Sipariş öğrencisi bulunamadı.");
        var studentName = student.FullName;
        var now = timeProvider.GetTurkeyNow();
        if (student.AssignmentId is { } assignmentId && student.ProductUnitId is { } productUnitId)
        {
            var assignment = await repository.GetRentalAssignmentAsync(assignmentId, cancellationToken)
                ?? throw new ResourceNotFoundException("Öğrenci kit ataması bulunamadı.");
            var unit = await repository.GetProductUnitAsync(productUnitId, cancellationToken)
                ?? throw new ResourceNotFoundException("Öğrenciye atanmış fiziksel kit bulunamadı.");
            if (assignment.Status != RentalAssignmentStatus.Reserved || unit.Status != ProductUnitStatus.Reserved)
                throw new ConflictException("order.student_remove_locked", "Hazırlığı başlayan veya teslim edilen öğrenciler siparişten silinemez.");
            assignment.Cancel();
            unit.ReleaseReservation(actorId, now);
            await AddActivityAsync(unit.Id, assignment.Id, order.Id, student.Id, actorId, actorId.ToString(),
                "Kit siparişten çıkarıldı", $"{studentName} öğrencisi silinirken kit siparişten çıkarıldı.", cancellationToken, now);
        }
        order.RemoveOneKitRequirement(student.ProductModelId);
        cohort!.RemoveStudent(studentId);
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), actorId, nameof(RentalCohort), cohort.Id,
            "StudentRemovedFromOrder", studentName, order.OrderNumber, timeProvider.GetTurkeyNow()), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<OrderDetailResponse> ConfirmStudentDeliveryAsync(Guid orderId, Guid studentId, Guid actorId,
        CancellationToken cancellationToken)
    {
        return await ConfirmStudentDeliveriesAsync(orderId, [studentId], actorId, cancellationToken);
    }

    public async Task<OrderDetailResponse> ConfirmStudentDeliveriesAsync(Guid orderId,
        IReadOnlyCollection<Guid> studentIds, Guid actorId, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(orderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Sipariş bulunamadı.");
        if (order.Type != OrderType.Rental)
            throw new ConflictException("order.student_delivery_not_allowed", "Öğrenci teslimi yalnızca kiralama siparişlerinde yapılabilir.");
        var selectedStudentIds = studentIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (selectedStudentIds.Length == 0)
            throw new ConflictException("order.students_required", "Teslim için en az bir öğrenci seçmelisiniz.");

        var cohort = (await repository.GetRentalCohortsAsync(order.CustomerId, cancellationToken))
            .FirstOrDefault(item => item.Students.Any(student => selectedStudentIds.Contains(student.Id) && student.OrderId == orderId));
        var selectedStudents = cohort?.Students
            .Where(item => selectedStudentIds.Contains(item.Id) && !item.IsDeleted && item.OrderId == orderId)
            .ToArray() ?? [];
        if (selectedStudents.Length != selectedStudentIds.Length)
            throw new ResourceNotFoundException("Sipariş öğrencilerinden biri veya birkaçı bulunamadı.");

        var deliveredAssignmentIds = (await repository.GetKitLocationEventsAsync(cancellationToken))
            .Where(item => item.OrderId == orderId && item.Source == KitLocationEventSource.DeliveryReceipt && item.AssignmentId.HasValue)
            .Select(item => item.AssignmentId!.Value)
            .ToHashSet();
        var now = timeProvider.GetTurkeyNow();
        foreach (var student in selectedStudents)
        {
            if (!student.HasAddress)
                throw new ConflictException("order.student_address_incomplete", "Teslim işaretlemek için seçilen öğrencilerin adresi girilmiş olmalıdır.");
            if (!student.AssignmentId.HasValue || !student.ProductUnitId.HasValue)
                throw new ConflictException("order.student_kit_incomplete", "Teslim işaretlemek için seçilen öğrencilere fiziksel kit atanmış olmalıdır.");
            if (student.AssignmentId is { } assignmentId && deliveredAssignmentIds.Contains(assignmentId)) continue;

            await AddStudentKitLocationEventAsync(student, student.ProductUnitId.Value, student.AssignmentId.Value,
                order.Id, order.CustomerId, actorId, now, cancellationToken);
            await AddActivityAsync(student.ProductUnitId.Value, student.AssignmentId.Value, order.Id, student.Id,
                actorId, actorId.ToString(), "Öğrenciye teslim edildi",
                $"Kit {student.FullName} öğrencisine teslim edildi ve adresi kit konumuna işlendi.", cancellationToken, now);
            deliveredAssignmentIds.Add(student.AssignmentId.Value);
        }

        await AuditAsync(actorId, nameof(RentalCohortStudent), selectedStudentIds[0], "StudentDeliveryConfirmed",
            null, $"{selectedStudentIds.Length} öğrenci", cancellationToken);

        return await GetOrderDetailAsync(orderId, cancellationToken);
    }

    public async Task<PublicStudentAddressContextResponse> GetPublicStudentAddressContextAsync(string token,
        CancellationToken cancellationToken)
    {
        var (cohort, student) = await GetStudentByAddressTokenAsync(token, cancellationToken);
        var customer = await repository.GetCustomerAsync(cohort.CustomerId, cancellationToken);
        var order = student.OrderId.HasValue ? await repository.GetOrderAsync(student.OrderId.Value, cancellationToken) : null;
        var productModel = await repository.GetProductModelAsync(student.ProductModelId, cancellationToken);
        return new PublicStudentAddressContextResponse(student.FullName, student.GuardianPhone,
            customer?.Name ?? "Müşteri", order?.OrderNumber ?? cohort.Name,
            productModel?.Name ?? "Eğitim kiti", student.AddressLine, student.Latitude, student.Longitude);
    }

    public async Task SavePublicStudentAddressAsync(SavePublicStudentAddressCommand command,
        CancellationToken cancellationToken)
    {
        var (cohort, student) = await GetStudentByAddressTokenAsync(command.Token, cancellationToken);
        var now = timeProvider.GetTurkeyNow();
        cohort.UpdateStudentAddressByToken(command.Token, command.AddressLine, command.Latitude, command.Longitude,
            now);
        var order = student.OrderId.HasValue
            ? await repository.GetOrderAsync(student.OrderId.Value, cancellationToken)
            : null;
        if (order?.Status == RentalOrderStatus.Completed && student.HasKitAssignment &&
            student.AssignmentId.HasValue && student.ProductUnitId.HasValue && student.HasAddress)
        {
            await AddStudentKitLocationEventAsync(student, student.ProductUnitId.Value, student.AssignmentId.Value,
                order.Id, cohort.CustomerId, PublicActorId, now, cancellationToken);
            await AddActivityAsync(student.ProductUnitId.Value, student.AssignmentId.Value, order.Id,
                student.Id, PublicActorId, "Public Form", "Öğrenci adresi güncellendi",
                $"{student.FullName} öğrencisinin adresi public formdan kaydedildi.", cancellationToken, now);
        }
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), PublicActorId, nameof(RentalCohort),
            cohort.Id, "StudentAddressSubmitted", student.FullName, student.OrderId?.ToString(), now),
            cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<(RentalCohort Cohort, RentalCohortStudent Student)> GetStudentByAddressTokenAsync(
        string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ResourceNotFoundException("Adres formu bağlantısı geçersiz.");
        var normalizedToken = token.Trim();
        var cohort = await repository.GetRentalCohortByStudentAddressTokenAsync(normalizedToken, cancellationToken)
            ?? throw new ResourceNotFoundException("Adres formu bağlantısı geçersiz.");
        var student = cohort.Students.SingleOrDefault(item =>
            !item.IsDeleted && string.Equals(item.PublicAddressToken, normalizedToken, StringComparison.Ordinal))
            ?? throw new ResourceNotFoundException("Adres formu bağlantısı geçersiz.");
        return (cohort, student);
    }

    public async Task<OrderKitPreparationResponse> CreateAndReserveOrderKitsAsync(Guid orderId,
        IReadOnlyCollection<OrderKitLineCommand> requestedLines, bool useAvailableKits, Guid actorId,
        CancellationToken cancellationToken, Guid? rentalCohortId = null, string? actorDisplayName = null,
        IReadOnlyCollection<Guid>? selectedStudentIds = null)
    {
        var order = await repository.GetOrderAsync(orderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Sipariş bulunamadı.");
        if (order.Status != RentalOrderStatus.Approved)
            throw new ConflictException("order.not_approved", "Fiziksel kitler yalnızca onaylanmış sipariş için oluşturulabilir.");

        var existingAssignments = await repository.GetAssignmentsForOrderAsync(order.Id, cancellationToken);
        var hasSelectedStudents = selectedStudentIds is { Count: > 0 };
        if ((!hasSelectedStudents && existingAssignments.Count > 0) || order.ProductUnits.Count > 0)
            throw new ConflictException("order.kits_already_created", "Bu siparişin fiziksel kitleri daha önce oluşturulmuş.");
        RentalCohort? cohort = null;
        IReadOnlyCollection<RentalCohortStudent> cohortStudents = [];
        if (rentalCohortId.HasValue)
        {
            cohort = await repository.GetRentalCohortAsync(rentalCohortId.Value, cancellationToken)
                ?? throw new ResourceNotFoundException("Kiralama dönemi bulunamadı.");
            if (cohort.CustomerId != order.CustomerId)
                throw new ForbiddenException("Seçilen dönem bu siparişin müşterisine ait değil.");
            var eligibleStudents = cohort.Students.Where(item => !item.IsDeleted && !item.HasKitAssignment).ToArray();
            if (hasSelectedStudents && selectedStudentIds!.Any(studentId =>
                    !eligibleStudents.Any(student => student.Id == studentId)))
                throw new ConflictException("rental_cohort.student_selection_invalid",
                    "Seçilen öğrencilerden biri kit oluşturma için uygun değil.");
            cohortStudents = hasSelectedStudents
                ? eligibleStudents.Where(student => selectedStudentIds!.Contains(student.Id)).ToArray()
                : eligibleStudents;
            if (cohortStudents.Count == 0)
                throw new ConflictException("rental_cohort.no_students", "Seçilen dönemde kit atanacak öğrenci yok.");
            requestedLines = cohortStudents
                .GroupBy(item => item.ProductModelId)
                .Select(group => new OrderKitLineCommand(group.Key, group.Count()))
                .ToArray();
        }
        else if (order.Type == OrderType.Rental)
        {
            cohort = (await repository.GetRentalCohortsAsync(order.CustomerId, cancellationToken))
                .FirstOrDefault(item => item.Students.Any(student => student.OrderId == order.Id));
            if (cohort is not null)
            {
                cohortStudents = cohort.Students.Where(item => !item.IsDeleted && !item.HasKitAssignment).ToArray();
                requestedLines = cohortStudents
                    .GroupBy(item => item.ProductModelId)
                    .Select(group => new OrderKitLineCommand(group.Key, group.Count()))
                    .ToArray();
            }
        }
        var lines = requestedLines
            .Where(line => line.ProductModelId != Guid.Empty && line.Quantity > 0)
            .GroupBy(line => line.ProductModelId)
            .Select(group => new OrderKitLineCommand(group.Key, group.Sum(line => line.Quantity)))
            .ToArray();
        if (lines.Length == 0)
            throw new ConflictException("order.lines_required", "En az bir kit ve adet seçilmelidir.");
        var models = new Dictionary<Guid, ProductModel>();
        foreach (var requestedLine in lines)
        {
            var model = await repository.GetProductModelAsync(requestedLine.ProductModelId, cancellationToken)
                ?? throw new ResourceNotFoundException("Seçilen eğitim kitlerinden biri bulunamadı.");
            models[model.Id] = model;
        }
        if (hasSelectedStudents)
        {
            if (cohort is null || order.Type != OrderType.Rental)
                throw new ConflictException("rental_cohort.student_selection_invalid",
                    "Öğrenci seçimi yalnızca kiralama siparişlerinde kullanılabilir.");
        }
        else
        {
            order.ReplaceLines(lines.Select(line => (line.ProductModelId, line.Quantity)).ToArray());
        }
        var preparationLines = lines.Select(line =>
        {
            var orderLine = order.Lines.FirstOrDefault(item => item.ProductModelId == line.ProductModelId);
            return orderLine is null
                ? throw new ConflictException("order.line_not_found", "Seçilen kit sipariş satırında bulunamadı.")
                : (Line: line, OrderLine: orderLine);
        }).ToArray();

        var now = timeProvider.GetTurkeyNow();
        var units = new List<ProductUnit>();
        var createdUnits = new List<ProductUnit>();
        var assignments = new List<RentalAssignment>();
        var purchaseLinks = new List<(Guid OrderLineId, Guid ProductUnitId)>();
        var availableUnitsByModel = (useAvailableKits || order.Type == OrderType.Purchase)
            ? (await repository.GetProductUnitsAsync(cancellationToken))
                .Where(unit => unit.Status == ProductUnitStatus.Available)
                .GroupBy(unit => unit.ProductModelId)
                .ToDictionary(group => group.Key,
                    group => new Queue<ProductUnit>(group.OrderBy(unit => unit.SerialNumber)))
            : [];
        foreach (var preparationLine in preparationLines)
        {
            for (var index = 0; index < preparationLine.Line.Quantity; index++)
            {
                ProductUnit unit;
                if (availableUnitsByModel.TryGetValue(preparationLine.Line.ProductModelId, out var availableUnits) &&
                    availableUnits.TryDequeue(out var availableUnit))
                {
                    unit = availableUnit;
                }
                else
                {
                    var unitId = Guid.NewGuid();
                    var serialNumber = ProductUnitSerialNumber.Create(models[preparationLine.Line.ProductModelId].Sku, now, unitId);
                    unit = ProductUnit.Create(unitId, preparationLine.Line.ProductModelId, serialNumber,
                        $"KITRENTAL:{serialNumber}", actorId, now);
                    createdUnits.Add(unit);
                }
                units.Add(unit);
                if (order.Type == OrderType.Rental)
                    assignments.Add(RentalAssignment.Create(Guid.NewGuid(), preparationLine.OrderLine.Id, order.CustomerId, unit.Id,
                        now, actorId));
                else
                    purchaseLinks.Add((preparationLine.OrderLine.Id, unit.Id));
            }
        }

        var stockMovements = await stockConsumptionPlanner.CreateMovementsAsync(
            createdUnits.GroupBy(unit => unit.ProductModelId)
                .Select(group => new ProductUnitProduction(models[group.Key], group.Select(unit => unit.Id).ToArray()))
                .ToArray(), actorId, now, cancellationToken);
        var creationAudits = createdUnits.Select(unit => new AuditEntry(Guid.NewGuid(), actorId, nameof(ProductUnit), unit.Id,
            "CreatedForOrder", null, order.OrderNumber, now)).ToArray();
        try
        {
            await repository.AddProductUnitsWithStockConsumptionAsync(
                createdUnits, stockMovements, creationAudits, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflictException("product_unit.identifier_not_unique", exception.Message);
        }

        if (order.Type == OrderType.Purchase)
            foreach (var link in purchaseLinks)
                order.AddProductUnit(link.OrderLineId, link.ProductUnitId);
        foreach (var unit in createdUnits)
            await AddActivityAsync(unit.Id, null, order.Id, null, actorId, actorDisplayName ?? actorId.ToString(),
                "Kit oluşturuldu", $"Kit {order.OrderNumber} siparişi için oluşturuldu.", cancellationToken, now);
        foreach (var unit in units.Except(createdUnits))
            await AddActivityAsync(unit.Id, null, order.Id, null, actorId, actorDisplayName ?? actorId.ToString(),
                "Kit rezerve edildi", $"Hazır kit {order.OrderNumber} siparişi için rezerve edildi.",
                cancellationToken, now);
        var reserved = order.Type == OrderType.Rental
            ? await repository.TryCreateReservationsAsync(units, assignments, actorId, now, cancellationToken)
            : await repository.TryReserveUnitsAsync(units, actorId, now, cancellationToken);
        if (!reserved)
        {
            if (order.Type == OrderType.Purchase)
                order.ClearProductUnits();
            foreach (var unit in createdUnits)
                await repository.RemoveProductUnitAsync(unit, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            throw new ConflictException("order.kit_reservation_failed", "Sipariş kitleri rezerve edilemedi.");
        }

        if (cohort is not null && order.Type == OrderType.Rental)
        {
            var assignmentsByModel = assignments
                .Join(units, assignment => assignment.ProductUnitId, unit => unit.Id,
                    (assignment, unit) => new { assignment, unit })
                .GroupBy(item => item.unit.ProductModelId)
                .ToDictionary(group => group.Key,
                    group => new Queue<(RentalAssignment Assignment, ProductUnit Unit)>(
                        group.OrderBy(item => item.unit.SerialNumber)
                            .Select(item => (item.assignment, item.unit))));
            foreach (var student in cohortStudents.OrderBy(item => item.FullName))
            {
                if (!assignmentsByModel.TryGetValue(student.ProductModelId, out var queue) ||
                    !queue.TryDequeue(out var match))
                    throw new ConflictException("rental_cohort.assignment_failed",
                        "Öğrenci kit ataması tamamlanamadı.");
                cohort.LinkStudentToKit(student.Id, order.Id, match.Assignment.Id, match.Unit.Id);
                await AddActivityAsync(match.Unit.Id, match.Assignment.Id, order.Id, student.Id, actorId,
                    actorDisplayName ?? actorId.ToString(), "Öğrenciye atandı",
                    $"Kit {student.FullName} öğrencisine atandı.", cancellationToken, now);
            }
        }

        await AuditAsync(actorId, nameof(RentalOrder), order.Id, "OrderKitsCreated", null,
            $"Created:{createdUnits.Count}|Reused:{units.Count - createdUnits.Count}", cancellationToken);
        var linkByUnit = order.Type == OrderType.Rental
            ? assignments.ToDictionary(item => item.ProductUnitId, item => item.Id)
            : order.ProductUnits.ToDictionary(item => item.ProductUnitId, item => item.Id);
        return new OrderKitPreparationResponse(order.Id, createdUnits.Count, units.Count - createdUnits.Count, units.Select(unit =>
            new OrderKitResponse(unit.Id, linkByUnit[unit.Id], unit.ProductModelId,
                unit.SerialNumber, unit.Status)).ToArray());
    }

    public async Task<RentalOrder> TransitionOrderAsync(Guid orderId, RentalOrderStatus target, Guid actorId, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(orderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Sipariş bulunamadı.");
        var now = timeProvider.GetTurkeyNow();
        var previous = order.Status;
        var assignments = await repository.GetAssignmentsForOrderAsync(order.Id, cancellationToken);
        var deliveryEvents = await repository.GetKitLocationEventsAsync(cancellationToken);
        var allocatedUnitIds = order.Type == OrderType.Rental
            ? assignments.Select(item => item.ProductUnitId).ToArray()
            : order.ProductUnits.Select(item => item.ProductUnitId).ToArray();
        if (target is RentalOrderStatus.Preparing or RentalOrderStatus.OutboundInTransit or RentalOrderStatus.Delivered or RentalOrderStatus.Completed)
        {
            var requestedKitCount = order.Lines.Sum(line => line.Quantity);
            if (allocatedUnitIds.Length != requestedKitCount)
                throw new ConflictException("order.kits_incomplete",
                    $"Siparişin {requestedKitCount} fiziksel kitinin tamamı oluşturulup rezerve edilmelidir.");
        }
        switch (target)
        {
            case RentalOrderStatus.Approved:
                order.Approve(actorId, now);
                break;
            case RentalOrderStatus.Preparing:
                order.StartPreparation(actorId, now);
                foreach (var unitId in allocatedUnitIds)
                {
                    var unit = await repository.GetProductUnitAsync(unitId, cancellationToken);
                    if (unit?.Status == ProductUnitStatus.Reserved)
                        unit.StartPreparation(actorId, now);
                }
                if (order.Type == OrderType.Rental)
                    foreach (var assignment in assignments.Where(item => item.Status == RentalAssignmentStatus.Reserved))
                        assignment.Activate();
                break;
            case RentalOrderStatus.ReadyToShip: order.MarkReadyToShip(actorId, now); break;
            case RentalOrderStatus.OutboundInTransit:
                if (order.Status == RentalOrderStatus.Preparing)
                    order.MarkReadyToShip(actorId, now);
                order.Dispatch(actorId, now);
                foreach (var unitId in allocatedUnitIds)
                {
                    var unit = await repository.GetProductUnitAsync(unitId, cancellationToken);
                    if (unit?.Status == ProductUnitStatus.Reserved)
                        unit.StartPreparation(actorId, now);
                    if (unit?.Status == ProductUnitStatus.Preparing)
                        unit.Dispatch(actorId, now);
                }
                break;
            case RentalOrderStatus.Delivered:
                var deliveredOrderStudents = await GetOrderStudentsForCompletionAsync(order, cancellationToken);
                EnsureOrderStudentsReadyForCompletion(deliveredOrderStudents, deliveryEvents);
                order.ConfirmDelivery(actorId, now);
                foreach (var unitId in allocatedUnitIds)
                {
                    var unit = await repository.GetProductUnitAsync(unitId, cancellationToken);
                    if (unit?.Status == ProductUnitStatus.OutboundInTransit)
                    {
                        if (order.Type == OrderType.Purchase) unit.CompleteSale(actorId, now);
                        else unit.ConfirmDelivery(actorId, now);
                    }
                }
                if (order.Type == OrderType.Rental)
                    foreach (var assignment in assignments.Where(item => item.Status == RentalAssignmentStatus.Reserved))
                        assignment.Activate();
                await AddStudentKitLocationEventsForCompletionAsync(order, deliveredOrderStudents, actorId, now,
                    cancellationToken);
                order.LockAfterDelivery(actorId, now);
                break;
            case RentalOrderStatus.Completed:
                var orderStudents = await GetOrderStudentsForCompletionAsync(order, cancellationToken);
                EnsureOrderStudentsReadyForCompletion(orderStudents, deliveryEvents);
                foreach (var unitId in allocatedUnitIds)
                {
                    var unit = await repository.GetProductUnitAsync(unitId, cancellationToken);
                    if (unit is null) continue;
                    if (order.Type == OrderType.Purchase)
                    {
                        if (unit.Status is ProductUnitStatus.Reserved or ProductUnitStatus.Preparing or ProductUnitStatus.OutboundInTransit)
                            unit.CompleteSaleFulfillment(actorId, now);
                    }
                    else if (unit.Status is ProductUnitStatus.Reserved or ProductUnitStatus.Preparing or ProductUnitStatus.OutboundInTransit)
                    {
                        unit.CompleteRentalFulfillment(actorId, now);
                    }
                }
                if (order.Type == OrderType.Rental)
                    foreach (var assignment in assignments.Where(item => item.Status == RentalAssignmentStatus.Reserved))
                        assignment.Activate();
                await AddStudentKitLocationEventsForCompletionAsync(order, orderStudents, actorId, now,
                    cancellationToken);
                order.CompleteFulfillment(actorId, now);
                break;
            case RentalOrderStatus.AwaitingReturn:
                if (order.Type != OrderType.Rental)
                    throw new ConflictException("order.purchase_return_not_allowed", "Satın alma siparişinde kiralama iadesi başlatılamaz.");
                order.RequestReturn(actorId, now);
                break;
            default: throw new ConflictException("order.unsupported_transition", "Bu durum geçişi ilgili süreç üzerinden yapılmalıdır.");
        }
        await AuditAsync(actorId, nameof(RentalOrder), order.Id, "StatusChanged", previous.ToString(), order.Status.ToString(), cancellationToken);
        return order;
    }

    public async Task<FaultTicket> OpenFaultAsync(OpenFaultCommand command, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(command.OrderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Sipariş bulunamadı.");
        var assignment = await repository.GetRentalAssignmentAsync(command.AssignmentId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kiralama ataması bulunamadı.");
        if (order.CustomerId != command.CustomerId || assignment.CustomerId != command.CustomerId || assignment.ProductUnitId != command.ProductUnitId)
            throw new ForbiddenException("Arıza kaydı müşteri, sipariş ve ürün atamasıyla eşleşmiyor.");

        var now = timeProvider.GetTurkeyNow();
        var ticket = FaultTicket.Open(
            Guid.NewGuid(), $"FLT-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..21], command.CustomerId, command.OrderId,
            command.AssignmentId, command.ProductUnitId, command.Category, command.Severity, command.Description, now,
            command.ReporterName, command.ReporterPhone, command.ReporterAddress, command.Latitude, command.Longitude,
            command.Origin, command.AttachmentUrl);
        await repository.AddFaultTicketAsync(ticket, cancellationToken);
        await AddActivityAsync(command.ProductUnitId, command.AssignmentId, command.OrderId, null, command.ActorId,
            command.ReporterName ?? command.ActorId.ToString(), "Arıza kaydı oluşturuldu",
            string.IsNullOrWhiteSpace(command.ReporterName)
                ? "Arıza kaydı oluşturuldu."
                : $"{command.ReporterName.Trim()} arıza kaydı oluşturdu.",
            cancellationToken, now);
        await AuditAsync(command.ActorId, nameof(FaultTicket), ticket.Id, "Opened", null, ticket.Status.ToString(), cancellationToken);
        return ticket;
    }

    public async Task<PublicFaultKitResponse> GetPublicFaultKitAsync(string qrCode,
        CancellationToken cancellationToken)
    {
        var unit = await repository.GetProductUnitByQrCodeAsync(qrCode, cancellationToken)
            ?? throw new ResourceNotFoundException("Bu QR kodla eşleşen fiziksel kit bulunamadı.");
        var model = await repository.GetProductModelAsync(unit.ProductModelId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kit modeli bulunamadı.");
        return new PublicFaultKitResponse(unit.QrCode, unit.Id, model.Name, unit.SerialNumber);
    }

    public async Task<PublicKitDeliveryContextResponse> GetPublicKitDeliveryContextAsync(string qrCode,
        CancellationToken cancellationToken)
    {
        var unit = await repository.GetProductUnitByQrCodeAsync(qrCode, cancellationToken)
            ?? throw new ResourceNotFoundException("Bu QR kodla eşleşen fiziksel kit bulunamadı.");
        var assignment = (await repository.GetAssignmentsForProductUnitAsync(unit.Id, cancellationToken))
            .Where(item => item.Status is RentalAssignmentStatus.Reserved or RentalAssignmentStatus.Active)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();
        if (assignment is null)
            return new PublicKitDeliveryContextResponse(null, null, null, null, null);

        var location = await repository.GetLatestKitLocationEventForAssignmentAsync(unit.Id, assignment.Id,
            cancellationToken);
        if (location is not null)
            return new PublicKitDeliveryContextResponse(location.ContactName, location.ContactPhone,
                location.AddressLine, location.Latitude, location.Longitude);

        var customer = await repository.GetCustomerAsync(assignment.CustomerId, cancellationToken);
        var student = (await repository.GetRentalCohortsAsync(assignment.CustomerId, cancellationToken))
            .SelectMany(item => item.Students)
            .FirstOrDefault(item => !item.IsDeleted &&
                (item.AssignmentId == assignment.Id ||
                    (!item.AssignmentId.HasValue && item.ProductUnitId == unit.Id)));
        var order = await repository.FindOrderByLineIdAsync(assignment.OrderLineId, cancellationToken);
        var customerAddress = customer?.Addresses.FirstOrDefault();
        return new PublicKitDeliveryContextResponse(
            FirstNotEmpty(student?.FullName, order?.DeliveryAddress.ContactName, customerAddress?.ContactName),
            FirstNotEmpty(student?.GuardianPhone, order?.DeliveryAddress.Phone, customerAddress?.Phone),
            FirstNotEmpty(student?.AddressLine, order?.DeliveryAddress.Line1, customerAddress?.Line1),
            student?.Latitude,
            student?.Longitude);
    }

    public async Task<PublicFaultContextResponse> GetPublicFaultContextAsync(string qrCode,
        CancellationToken cancellationToken)
    {
        var unit = await repository.GetProductUnitByQrCodeAsync(qrCode, cancellationToken)
            ?? throw new ResourceNotFoundException("Bu QR kodla eşleşen fiziksel kit bulunamadı.");
        var ticket = await repository.GetOpenFaultTicketAsync(unit.Id, cancellationToken);
        return ticket is null
            ? new PublicFaultContextResponse(null, null, null, null, null, null, null, null)
            : new PublicFaultContextResponse(ticket.Id, ticket.ReporterName, ticket.ReporterPhone,
                ticket.ReporterAddress, ticket.Description, ticket.Latitude, ticket.Longitude, ticket.AttachmentUrl);
    }

    public async Task<FaultTicket> UpdatePublicFaultAsync(Guid faultId, string qrCode, string reporterName,
        string reporterPhone, string reporterAddress, string description,
        double? latitude, double? longitude, string? attachmentUrl,
        CancellationToken cancellationToken)
    {
        var unit = await repository.GetProductUnitByQrCodeAsync(qrCode, cancellationToken)
            ?? throw new ResourceNotFoundException("Bu QR kodla eşleşen fiziksel kit bulunamadı.");
        var ticket = await repository.GetFaultTicketAsync(faultId, cancellationToken)
            ?? throw new ResourceNotFoundException("Arıza kaydı bulunamadı.");
        if (ticket.ProductUnitId != unit.Id || ticket.Status is FaultStatus.Resolved or FaultStatus.RemoteResolved or FaultStatus.Rejected or FaultStatus.Closed)
            throw new ConflictException("fault.edit_not_allowed", "Bu arıza kaydı güncellenemez.");
        ticket.UpdatePublicDetails(ticket.Category, description, reporterName, reporterPhone, reporterAddress,
            CoordinatesAreValid(latitude, longitude) ? latitude : null,
            CoordinatesAreValid(latitude, longitude) ? longitude : null, attachmentUrl);
        var now = timeProvider.GetTurkeyNow();
        await repository.AddKitLocationEventAsync(KitLocationEvent.Create(Guid.NewGuid(), unit.Id, ticket.AssignmentId,
            ticket.OrderId, ticket.CustomerId, KitLocationEventSource.FaultUpdate, ticket.Id, reporterName,
            reporterPhone, reporterAddress,
            CoordinatesAreValid(latitude, longitude) ? latitude : null,
            CoordinatesAreValid(latitude, longitude) ? longitude : null, now, PublicActorId),
            cancellationToken);
        await AddActivityAsync(unit.Id, ticket.AssignmentId, ticket.OrderId, null, PublicActorId, reporterName,
            "Arıza kaydı güncellendi", $"{reporterName.Trim()} arıza kaydını güncelledi.",
            cancellationToken, now);
        await repository.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public async Task<FaultTicket> OpenPublicFaultAsync(OpenPublicFaultCommand command,
        CancellationToken cancellationToken)
    {
        var unit = await repository.GetProductUnitByQrCodeAsync(command.QrCode, cancellationToken)
            ?? throw new ResourceNotFoundException("Bu QR kodla eşleşen fiziksel kit bulunamadı.");
        var existing = await repository.GetOpenFaultTicketAsync(unit.Id, cancellationToken);
        if (existing is not null)
        {
            existing.UpdatePublicDetails("Son kullanıcı bildirimi", command.Description, command.ReporterName,
                command.ReporterPhone, command.ReporterAddress,
                CoordinatesAreValid(command.Latitude, command.Longitude) ? command.Latitude : null,
                CoordinatesAreValid(command.Latitude, command.Longitude) ? command.Longitude : null, command.AttachmentUrl);
            var now = timeProvider.GetTurkeyNow();
            await repository.AddKitLocationEventAsync(KitLocationEvent.Create(Guid.NewGuid(), unit.Id,
                existing.AssignmentId, existing.OrderId, existing.CustomerId, KitLocationEventSource.FaultUpdate,
                existing.Id, command.ReporterName, command.ReporterPhone, command.ReporterAddress,
                CoordinatesAreValid(command.Latitude, command.Longitude) ? command.Latitude : null,
                CoordinatesAreValid(command.Latitude, command.Longitude) ? command.Longitude : null, now, PublicActorId),
                cancellationToken);
            await AddActivityAsync(unit.Id, existing.AssignmentId, existing.OrderId, null, PublicActorId,
                command.ReporterName, "Arıza kaydı güncellendi",
                $"{command.ReporterName.Trim()} arıza kaydını güncelledi.", cancellationToken, now);
            await repository.SaveChangesAsync(cancellationToken);
            return existing;
        }
        var assignment = (await repository.GetAssignmentsForProductUnitAsync(unit.Id, cancellationToken))
            .Where(item => item.Status == RentalAssignmentStatus.Active)
            .FirstOrDefault()
            ?? throw new ConflictException("fault.no_active_rental", "Bu kit için aktif bir kiralama bulunmuyor.");
        var order = await repository.FindOrderByLineIdAsync(assignment.OrderLineId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kiralama siparişi bulunamadı.");
        var ticket = await OpenFaultAsync(new OpenFaultCommand(assignment.CustomerId, order.Id, assignment.Id, unit.Id,
            "Son kullanici bildirimi", FaultSeverity.Medium, command.Description,
            PublicActorId, command.ReporterName, command.ReporterPhone,
                command.ReporterAddress,
                CoordinatesAreValid(command.Latitude, command.Longitude) ? command.Latitude : null,
            CoordinatesAreValid(command.Latitude, command.Longitude) ? command.Longitude : null, FaultOrigin.PublicForm,
            command.AttachmentUrl),
            cancellationToken);
        await repository.AddKitLocationEventAsync(KitLocationEvent.Create(Guid.NewGuid(), unit.Id, assignment.Id,
            order.Id, assignment.CustomerId, KitLocationEventSource.FaultReport, ticket.Id, command.ReporterName,
            command.ReporterPhone, command.ReporterAddress,
            CoordinatesAreValid(command.Latitude, command.Longitude) ? command.Latitude : null,
            CoordinatesAreValid(command.Latitude, command.Longitude) ? command.Longitude : null,
            timeProvider.GetTurkeyNow(), PublicActorId),
            cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public async Task<KitLocationEvent> CreatePublicKitDeliveryAsync(CreatePublicKitDeliveryCommand command,
        CancellationToken cancellationToken)
    {
        var unit = await repository.GetProductUnitByQrCodeAsync(command.QrCode, cancellationToken)
            ?? throw new ResourceNotFoundException("Bu QR kodla eşleşen fiziksel kit bulunamadı.");
        if (unit.Status != ProductUnitStatus.OutboundInTransit)
            throw new ConflictException("kit_delivery.not_in_transit", "Yalnızca kargodaki kit teslim alınabilir.");

        var assignment = (await repository.GetAssignmentsForProductUnitAsync(unit.Id, cancellationToken))
            .Where(item => item.Status is RentalAssignmentStatus.Reserved or RentalAssignmentStatus.Active)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault()
            ?? throw new ConflictException("kit_delivery.no_reserved_rental", "Bu kit için teslim bekleyen kiralama bulunmuyor.");
        var order = await repository.FindOrderByLineIdAsync(assignment.OrderLineId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kiralama siparişi bulunamadı.");
        var now = timeProvider.GetTurkeyNow();
        var actorId = PublicActorId;
        var recipientName = command.RecipientName.Trim();
        var fullAddress = command.AddressLine.Trim();
        unit.ConfirmDeliveryTo(actorId, now, recipientName, fullAddress);
        if (assignment.Status == RentalAssignmentStatus.Reserved) assignment.Activate();
        var locationEvent = KitLocationEvent.Create(Guid.NewGuid(), unit.Id, assignment.Id,
            order.Id, assignment.CustomerId, KitLocationEventSource.DeliveryReceipt, null,
            command.RecipientName, command.RecipientPhone, command.AddressLine,
            CoordinatesAreValid(command.Latitude, command.Longitude) ? command.Latitude : null,
            CoordinatesAreValid(command.Latitude, command.Longitude) ? command.Longitude : null, now, actorId);
        await repository.AddKitLocationEventAsync(locationEvent, cancellationToken);
        await AddActivityAsync(unit.Id, assignment.Id, order.Id, null, actorId, recipientName,
            "Kit teslim alındı", $"{recipientName} kiti teslim aldı.", cancellationToken, now);
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), actorId, nameof(ProductUnit), unit.Id,
            "PublicDeliveryReceived", ProductUnitStatus.OutboundInTransit.ToString(), unit.Status.ToString(), now),
            cancellationToken);

        var orderAssignments = await repository.GetAssignmentsForOrderAsync(order.Id, cancellationToken);
        if (order.Status == RentalOrderStatus.OutboundInTransit &&
            orderAssignments.Count > 0 &&
            orderAssignments.All(item => item.Status == RentalAssignmentStatus.Active))
        {
            order.ConfirmDelivery(actorId, now);
            order.ActivateRental(actorId, now);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return locationEvent;
    }

    public Task<IReadOnlyCollection<FaultTicket>> GetFaultTicketsAsync(Guid? customerId, CancellationToken cancellationToken) =>
        repository.GetFaultTicketsAsync(customerId, cancellationToken);

    public async Task<IReadOnlyCollection<FaultGuideEntryResponse>> GetFaultGuideEntriesAsync(bool activeOnly,
        CancellationToken cancellationToken) =>
        await MapFaultGuideEntriesAsync(
            await repository.GetFaultGuideEntriesAsync(activeOnly, cancellationToken), cancellationToken);

    public async Task<IReadOnlyCollection<FaultGuideEntryResponse>> GetPublicFaultGuideEntriesAsync(string qrCode,
        CancellationToken cancellationToken)
    {
        var unit = await repository.GetProductUnitByQrCodeAsync(qrCode, cancellationToken)
            ?? throw new ResourceNotFoundException("Bu QR kodla eşleşen fiziksel kit bulunamadı.");
        var entries = (await repository.GetFaultGuideEntriesAsync(true, cancellationToken))
            .Where(item => item.ProductModelId is null || item.ProductModelId == unit.ProductModelId)
            .ToArray();
        return await MapFaultGuideEntriesAsync(entries, cancellationToken);
    }

    public async Task<FaultGuideEntryResponse> SaveFaultGuideEntryAsync(SaveFaultGuideEntryCommand command,
        CancellationToken cancellationToken)
    {
        FaultGuideEntry entry;
        var action = "FaultGuideUpdated";
        if (command.Id.HasValue)
        {
            entry = await repository.GetFaultGuideEntryAsync(command.Id.Value, cancellationToken)
                ?? throw new ResourceNotFoundException("Problem rehberi kaydi bulunamadi.");
            entry.Update(command.Title, command.Problem, command.Solution, command.DisplayOrder, command.IsActive,
                command.ProductModelId);
        }
        else
        {
            entry = FaultGuideEntry.Create(Guid.NewGuid(), command.Title, command.Problem, command.Solution,
                command.DisplayOrder, command.ProductModelId);
            if (!command.IsActive)
                entry.Update(command.Title, command.Problem, command.Solution, command.DisplayOrder, false,
                    command.ProductModelId);
            await repository.AddFaultGuideEntryAsync(entry, cancellationToken);
            action = "FaultGuideCreated";
        }
        await repository.SaveChangesAsync(cancellationToken);
        await AuditAsync(command.ActorId, nameof(FaultGuideEntry), entry.Id, action, null, entry.Title,
            cancellationToken);
        return (await MapFaultGuideEntriesAsync([entry], cancellationToken)).Single();
    }

    public async Task DeleteFaultGuideEntryAsync(Guid id, Guid actorId, CancellationToken cancellationToken)
    {
        var entry = await repository.GetFaultGuideEntryAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("Problem rehberi kaydi bulunamadi.");
        await repository.RemoveFaultGuideEntryAsync(entry, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        await AuditAsync(actorId, nameof(FaultGuideEntry), id, "FaultGuideDeleted", entry.Title, null,
            cancellationToken);
    }

    public async Task<FaultPageResponse> GetFaultPageAsync(FaultPageQuery query,
        CancellationToken cancellationToken)
    {
        var customers = (await repository.GetCustomersAsync(cancellationToken)).ToDictionary(item => item.Id);
        var orders = (await repository.GetOrdersAsync(null, cancellationToken)).ToDictionary(item => item.Id);
        var tickets = (await repository.GetFaultTicketsAsync(query.CustomerId, cancellationToken))
            .Where(ticket => (!query.OrderId.HasValue || ticket.OrderId == query.OrderId) &&
                OperationsWorkload.MatchesFaultStage(ticket.Status, query.Stage));
        var items = tickets.Select(ticket =>
        {
            customers.TryGetValue(ticket.CustomerId, out var customer);
            orders.TryGetValue(ticket.OrderId, out var order);
            var reporterName = !string.IsNullOrWhiteSpace(ticket.ReporterName) ? ticket.ReporterName : order?.DeliveryAddress.ContactName
                ?? customer?.Addresses.FirstOrDefault()?.ContactName
                ?? customer?.Name
                ?? "-";
            var reporterPhone = !string.IsNullOrWhiteSpace(ticket.ReporterPhone) ? ticket.ReporterPhone : order?.DeliveryAddress.Phone
                ?? customer?.Addresses.FirstOrDefault()?.Phone
                ?? "-";
            var reporterAddress = !string.IsNullOrWhiteSpace(ticket.ReporterAddress) ? ticket.ReporterAddress : order?.DeliveryAddress.Line1
                ?? customer?.Addresses.FirstOrDefault()?.Line1
                ?? "-";
            return new FaultListItemResponse(ticket.Id, ticket.Number, ticket.CustomerId,
                customer?.Name ?? "Müşteri", reporterName, reporterPhone, reporterAddress, ticket.Category, ticket.Severity,
                ticket.Description, ticket.Status, ticket.OpenedAt, ticket.ApprovalStatus, ticket.Origin,
                ticket.AttachmentUrl, ticket.OrderId, order?.OrderNumber, ticket.ProductUnitId,
                Shipments: ticket.KargonomiShipments.OrderByDescending(shipment => shipment.CreatedAt)
                    .Select(KargonomiShippingService.MapFault).ToArray());
        });

        if (query.CustomerId.HasValue)
            items = items.Where(item => item.CustomerId == query.CustomerId.Value);

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            var term = query.Query.Trim();
            items = items.Where(item =>
                item.Number.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.ReporterName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.ReporterPhone.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.ReporterAddress.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.Category.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.Description.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        if (query.Status.HasValue)
            items = items.Where(item => item.Status == query.Status.Value);
        if (query.Severity.HasValue)
            items = items.Where(item => item.Severity == query.Severity.Value);
        if (query.OpenedFrom.HasValue)
            items = items.Where(item => TurkeyTime.DateOf(item.OpenedAt) >= query.OpenedFrom.Value);
        if (query.OpenedTo.HasValue)
            items = items.Where(item => TurkeyTime.DateOf(item.OpenedAt) <= query.OpenedTo.Value);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 10, 100);
        var ordered = items.OrderByDescending(item => item.OpenedAt).ToArray();
        var totalPages = Math.Max(1, (int)Math.Ceiling(ordered.Length / (double)pageSize));
        page = Math.Min(page, totalPages);
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        var units = (await repository.GetProductUnitsByIdsAsync(
            pageItems.Select(item => item.ProductUnitId).Distinct().ToArray(), cancellationToken))
            .ToDictionary(unit => unit.Id);
        return new FaultPageResponse(page, pageSize, ordered.Length, totalPages,
            pageItems.Select(item => item with
            {
                SerialNumber = units.TryGetValue(item.ProductUnitId, out var unit) ? unit.SerialNumber : null
            }).ToArray());
    }

    public async Task<FaultKitLabelResponse> GetFaultKitLabelAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await repository.GetFaultTicketAsync(ticketId, cancellationToken)
            ?? throw new ResourceNotFoundException("Arıza kaydı bulunamadı.");
        var unit = await repository.GetProductUnitAsync(ticket.ProductUnitId, cancellationToken)
            ?? throw new ResourceNotFoundException("Arızaya bağlı fiziksel kit bulunamadı.");
        var model = await repository.GetProductModelAsync(unit.ProductModelId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kit modeli bulunamadı.");
        return new(unit.Id, model.Name, model.Sku, unit.SerialNumber, unit.QrCode,
            ticket.ReporterName, ticket.ReporterPhone, ticket.ReporterAddress);
    }

    public async Task<FaultTicket> ChangeFaultStatusAsync(Guid ticketId, FaultStatus status, Guid actorId, string? note, CancellationToken cancellationToken)
    {
        var ticket = await repository.GetFaultTicketAsync(ticketId, cancellationToken)
            ?? throw new ResourceNotFoundException("Arıza kaydı bulunamadı.");
        var previous = ticket.Status;
        note = string.IsNullOrWhiteSpace(note) ? status switch
        {
            FaultStatus.Investigating => "Arıza incelemeye alındı.", FaultStatus.Accepted => "Arıza kabul edildi.",
            FaultStatus.Rejected => "Arıza reddedildi.", FaultStatus.RemoteResolved => "Uzaktan destekle çözüldü.",
            FaultStatus.AwaitingWorkshopShipment => "Atölye kargosu bekleniyor.", FaultStatus.WorkshopShipmentInTransit => "Kit atölyeye kargolandı.",
            FaultStatus.WorkshopReceived => "Kit atölyeye ulaştı.", FaultStatus.Repaired => "Arıza giderildi.",
            FaultStatus.Closed => "Kargo teslim edildi, arıza kapatıldı.", _ => "Arıza süreci güncellendi."
        } : note.Trim();
        var now = timeProvider.GetTurkeyNow();
        switch (status)
        {
            case FaultStatus.Investigating: ticket.MarkInvestigating(actorId, now, note); break;
            case FaultStatus.Accepted: ticket.Accept(actorId, now, note); break;
            case FaultStatus.Rejected: ticket.Reject(actorId, now, note); break;
            case FaultStatus.RemoteResolved: ticket.ResolveRemotely(actorId, now, note); break;
            case FaultStatus.AwaitingWorkshopShipment: ticket.AwaitWorkshopShipment(actorId, now, note); break;
            case FaultStatus.WorkshopShipmentInTransit: ticket.MarkWorkshopShipmentInTransit(actorId, now, note); break;
            case FaultStatus.WorkshopReceived: ticket.MarkWorkshopReceived(actorId, now, note); break;
            case FaultStatus.Repaired: ticket.MarkRepaired(actorId, now, note); break;
            case FaultStatus.Closed: ticket.Close(actorId, now, note); break;
            default: throw new ConflictException("fault.unsupported_transition", "Bu arıza süreci adımı artık kullanılamıyor.");
        }
        await AddActivityAsync(ticket.ProductUnitId, ticket.AssignmentId, ticket.OrderId, null, actorId,
            actorId.ToString(), "Arıza durumu güncellendi", note, cancellationToken);
        await AuditAsync(actorId, nameof(FaultTicket), ticket.Id, "StatusChanged", previous.ToString(), ticket.Status.ToString(), cancellationToken);
        return ticket;
    }

    public async Task<ReturnInspection> CompleteInspectionAsync(CompleteInspectionCommand command, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(command.OrderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Sipariş bulunamadı.");
        var unit = await repository.GetProductUnitAsync(command.ProductUnitId, cancellationToken)
            ?? throw new ResourceNotFoundException("Fiziksel ürün birimi bulunamadı.");
        var now = timeProvider.GetTurkeyNow();
        var inspection = ReturnInspection.Complete(
            Guid.NewGuid(), command.OrderId, command.ProductUnitId,
            command.Items.Select(item => new InspectionItem(Guid.NewGuid(), item.Name, item.IsPresent, item.IsDamaged, item.Note)).ToArray(),
            command.DamageCharge, command.Outcome, now, command.ActorId);
        unit.CompleteInspection(command.Outcome, command.ActorId, now, "İade kontrolü tamamlandı.");
        order.Complete(command.ActorId, now);
        await repository.AddInspectionAsync(inspection, cancellationToken);
        await AddActivityAsync(unit.Id, null, order.Id, null, command.ActorId, command.ActorId.ToString(),
            "İade kontrolü tamamlandı", $"İade kontrolü tamamlandı: {command.Outcome}.", cancellationToken, now);
        await AuditAsync(command.ActorId, nameof(ReturnInspection), inspection.Id, "Completed", null, command.Outcome.ToString(), cancellationToken);
        return inspection;
    }

    public async Task<IReadOnlyCollection<ReturnListItemResponse>> GetReturnsInProgressAsync(
        CancellationToken cancellationToken)
    {
        var customers = (await repository.GetCustomersAsync(cancellationToken)).ToDictionary(item => item.Id);
        var returns = await repository.GetKitReturnRequestsAsync(null, cancellationToken);
        return returns
            .Where(item => item.Status != KitReturnStatus.Received)
            .Select(item => new ReturnListItemResponse(
                item.Id,
                customers.TryGetValue(item.CustomerId, out var customer) ? customer.Name : "Müşteri",
                (int)item.Status,
                item.Carrier,
                item.TrackingNumber,
                item.CreatedAt,
                item.Items.Count,
                item.RequesterName,
                item.RequesterPhone,
                item.ReturnAddress,
                item.Latitude,
                item.Longitude,
                item.DeliveryMethod))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<ReturnTableItemResponse>> GetReturnsTableAsync(
        CancellationToken cancellationToken, Guid? customerId = null, Guid? orderId = null, string? state = null)
    {
        var orders = (await repository.GetOrdersAsync(customerId, cancellationToken))
            .Where(item => item.Type == OrderType.Rental && item.Period.HasValue &&
                (!orderId.HasValue || item.Id == orderId.Value))
            .ToArray();
        var orderIds = orders.Select(item => item.Id).ToHashSet();
        var orderByLineId = orders
            .SelectMany(order => order.Lines.Select(line => new { line.Id, Order = order }))
            .ToDictionary(item => item.Id, item => item.Order);
        var customers = (await repository.GetCustomersAsync(cancellationToken)).ToDictionary(item => item.Id);
        var models = (await repository.GetProductModelsAsync(cancellationToken)).ToDictionary(item => item.Id);
        var assignments = (await repository.GetAssignmentsForOrdersAsync(orderIds, cancellationToken))
            .Where(item => item.Status != RentalAssignmentStatus.Cancelled).ToArray();
        var units = (await repository.GetProductUnitsByIdsAsync(
            assignments.Select(item => item.ProductUnitId).Distinct().ToArray(), cancellationToken))
            .ToDictionary(item => item.Id);
        var studentsByAssignmentId = (await repository.GetRentalCohortsAsync(customerId, cancellationToken))
            .SelectMany(cohort => cohort.Students
                .Where(student => !student.IsDeleted && student.AssignmentId.HasValue)
                .Select(student => new { AssignmentId = student.AssignmentId!.Value, Student = student }))
            .GroupBy(item => item.AssignmentId)
            .ToDictionary(group => group.Key, group => group.First().Student);
        var latestReturnByAssignment = (await repository.GetKitReturnRequestsAsync(customerId, cancellationToken))
            .SelectMany(request => request.Items.Select(item => new { Request = request, Item = item }))
            .GroupBy(item => item.Item.AssignmentId)
            .ToDictionary(group => group.Key,
                group => group.OrderByDescending(item => item.Request.CreatedAt)
                    .ThenByDescending(item => item.Request.Id)
                    .First());
        var today = timeProvider.GetTurkeyToday();
        var result = new List<ReturnTableItemResponse>();

        foreach (var assignment in assignments)
        {
            if (!orderByLineId.TryGetValue(assignment.OrderLineId, out var order) ||
                !units.TryGetValue(assignment.ProductUnitId, out var unit) ||
                !models.TryGetValue(unit.ProductModelId, out var model))
                continue;
            var period = order.Period!.Value;

            latestReturnByAssignment.TryGetValue(assignment.Id, out var currentReturn);
            if (currentReturn is null &&
                (assignment.Status != RentalAssignmentStatus.Active || period.EndDate >= today ||
                 !OperationsWorkload.IsOperational(order.Status)))
                continue;

            studentsByAssignmentId.TryGetValue(assignment.Id, out var student);
            var returnStatus = currentReturn is null ? 0 : (int)currentReturn.Request.Status;
            var returnStateKey = OperationsWorkload.ReturnState(currentReturn?.Request);
            if (!string.IsNullOrWhiteSpace(state) && state != returnStateKey &&
                !(state == "missing-form" && currentReturn is null)) continue;
            var returnState = returnStateKey switch
            {
                "completed" => "Tamamlanmış",
                "in-transit" => "Kargoda",
                _ => "İade Bekleniyor"
            };
            var request = currentReturn?.Request;
            var addressLine = string.IsNullOrWhiteSpace(request?.ReturnAddress)
                ? student?.AddressLine
                : request.ReturnAddress;

            result.Add(new ReturnTableItemResponse(
                unit.Id,
                assignment.Id,
                request?.Id,
                student?.Id,
                customers.TryGetValue(order.CustomerId, out var customer) ? customer.Name : "Müşteri",
                student?.FullName ?? "-",
                student?.GuardianPhone ?? "-",
                model.Name,
                model.Sku,
                unit.SerialNumber,
                order.OrderNumber,
                period.StartDate,
                period.EndDate,
                (int)unit.Status,
                (int)assignment.Status,
                returnStatus,
                returnStateKey,
                returnState,
                request?.Carrier,
                request?.TrackingNumber,
                request?.ExternalShipmentId,
                request?.ExternalStatus,
                request?.ExternalStatusLabel,
                request?.KargonomiBarcode,
                request?.CreatedAt,
                request?.ShippedAt,
                request?.ReceivedAt,
                addressLine,
                student?.PublicAddressToken,
                request?.RequesterName,
                request?.RequesterPhone,
                request is null ? (int)KitReturnDeliveryMethod.PickupFromAddress : (int)request.DeliveryMethod, order.Id));
        }

        return result
            .OrderBy(item => item.ReturnStateKey == "completed" ? 2 : item.ReturnStateKey == "in-transit" ? 1 : 0)
            .ThenBy(item => item.EndDate)
            .ThenBy(item => item.StudentName)
            .ToArray();
    }

    private async Task ValidateOrderLinesAsync(IReadOnlyCollection<OrderLineCommand> lines,
        CancellationToken cancellationToken)
    {
        if (lines.Count == 0 || lines.Any(line => line.ProductModelId == Guid.Empty || line.Quantity <= 0))
            throw new ConflictException("order.lines_required", "Siparişte en az bir geçerli ürün satırı bulunmalıdır.");
        foreach (var line in lines)
            if (await repository.GetProductModelAsync(line.ProductModelId, cancellationToken) is null)
                throw new ResourceNotFoundException($"{line.ProductModelId} ürün modeli bulunamadı.");
    }

    private async Task<IReadOnlyCollection<FaultGuideEntryResponse>> MapFaultGuideEntriesAsync(
        IEnumerable<FaultGuideEntry> entries, CancellationToken cancellationToken)
    {
        var models = (await repository.GetProductModelsAsync(cancellationToken))
            .ToDictionary(item => item.Id);
        return entries.Select(entry => new FaultGuideEntryResponse(entry.Id, entry.Title, entry.Problem,
            entry.Solution, entry.DisplayOrder, entry.IsActive, entry.UpdatedAt, entry.ProductModelId,
            entry.ProductModelId is { } modelId && models.TryGetValue(modelId, out var model) ? model.Name : null))
            .ToArray();
    }

    private static bool CoordinatesAreValid(double? latitude, double? longitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    private static string? FirstNotEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private async Task AuditAsync(
        Guid actorId,
        string entityType,
        Guid entityId,
        string action,
        string? previousValue,
        string? newValue,
        CancellationToken cancellationToken)
    {
        await repository.AddAuditEntryAsync(
            new AuditEntry(Guid.NewGuid(), actorId, entityType, entityId, action, previousValue, newValue, timeProvider.GetTurkeyNow()),
            cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private Task AddActivityAsync(Guid productUnitId, Guid? assignmentId, Guid? orderId, Guid? studentId,
        Guid actorId, string actorDisplayName, string action, string description, CancellationToken cancellationToken,
        DateTimeOffset? occurredAt = null) =>
        repository.AddProductUnitActivityAsync(ProductUnitActivity.Create(Guid.NewGuid(), productUnitId,
            assignmentId, orderId, studentId, actorId, actorDisplayName, action, description,
            occurredAt ?? timeProvider.GetTurkeyNow()), cancellationToken);

    private async Task<RentalCohortStudent[]> GetOrderStudentsForCompletionAsync(RentalOrder order,
        CancellationToken cancellationToken)
    {
        if (order.Type != OrderType.Rental) return [];
        var cohort = (await repository.GetRentalCohortsAsync(order.CustomerId, cancellationToken))
            .FirstOrDefault(item => item.Students.Any(student => student.OrderId == order.Id));
        return cohort?.Students
            .Where(student => !student.IsDeleted && student.OrderId == order.Id)
            .ToArray() ?? [];
    }

    private static void EnsureOrderStudentsReadyForCompletion(IReadOnlyCollection<RentalCohortStudent> students,
        IReadOnlyCollection<KitLocationEvent> deliveryEvents)
    {
        if (students.Any(student => !student.HasAddress))
            throw new ConflictException("order.student_addresses_incomplete",
                "Siparişi tamamlamak için siparişteki tüm öğrencilerin kitleri teslim edilmiş olmalıdır.");
        if (students.Any(student => !student.HasKitAssignment ||
            !student.AssignmentId.HasValue || !student.ProductUnitId.HasValue))
            throw new ConflictException("order.student_kits_incomplete",
                "Siparişi tamamlamak için siparişteki tüm öğrencilere fiziksel kit atanmış olmalıdır.");
        if (students.Any(student => !deliveryEvents.Any(item => item.OrderId == student.OrderId &&
            item.AssignmentId == student.AssignmentId && item.Source == KitLocationEventSource.DeliveryReceipt)))
            throw new ConflictException("order.student_deliveries_incomplete",
                "Siparişi tamamlamak için siparişteki tüm öğrencilerin kitleri teslim edilmiş olmalıdır.");
    }

    private async Task AddStudentKitLocationEventsForCompletionAsync(RentalOrder order,
        IReadOnlyCollection<RentalCohortStudent> students, Guid actorId, DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var deliveredAssignmentIds = (await repository.GetKitLocationEventsAsync(cancellationToken))
            .Where(item => item.OrderId == order.Id && item.Source == KitLocationEventSource.DeliveryReceipt && item.AssignmentId.HasValue)
            .Select(item => item.AssignmentId!.Value)
            .ToHashSet();
        foreach (var student in students)
        {
            if (student.AssignmentId is { } assignmentId && deliveredAssignmentIds.Contains(assignmentId)) continue;
            await AddStudentKitLocationEventAsync(student, student.ProductUnitId!.Value,
                student.AssignmentId!.Value, order.Id, order.CustomerId, actorId, occurredAt,
                cancellationToken);
            await AddActivityAsync(student.ProductUnitId.Value, student.AssignmentId.Value, order.Id,
                student.Id, actorId, actorId.ToString(), "Öğrenci adresi konuma işlendi",
                $"{student.FullName} öğrencisinin adresi sipariş tamamlanırken kit konumuna işlendi.",
                cancellationToken, occurredAt);
        }
    }

    private Task AddStudentKitLocationEventAsync(RentalCohortStudent student, Guid productUnitId, Guid assignmentId,
        Guid orderId, Guid customerId, Guid actorId, DateTimeOffset occurredAt, CancellationToken cancellationToken) =>
        repository.AddKitLocationEventAsync(KitLocationEvent.Create(Guid.NewGuid(), productUnitId, assignmentId,
            orderId, customerId, KitLocationEventSource.DeliveryReceipt, student.Id, student.FullName,
            student.GuardianPhone, student.AddressLine, student.Latitude, student.Longitude, occurredAt, actorId),
            cancellationToken);
}











