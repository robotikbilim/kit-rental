using KitRental.Core.Application.Abstractions;
using KitRental.Core.Application.Common;
using KitRental.Core.Application.Operations;
using KitRental.Core.Domain.Auditing;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Rentals;
using KitRental.Core.Domain.Returns;
using KitRental.Core.Domain.Support;
using KitRental.SharedKernel;

namespace KitRental.Core.Application.CustomerPortal;

public sealed class CustomerPortalService(ICoreRepository repository, OperationsService operationsService)
{
    private static readonly Guid PublicActorId = new("00000000-0000-0000-0000-000000000001");

    public async Task<CustomerPortalResponse> GetOverviewAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(customerId, cancellationToken)
            ?? throw new ResourceNotFoundException("Müşteri hesabı bulunamadı.");
        var productModels = await repository.GetProductModelsAsync(cancellationToken);
        var modelLookup = productModels.ToDictionary(item => item.Id);
        var orders = await repository.GetOrdersAsync(customerId, cancellationToken);
        var kits = new List<PortalKitResponse>();
        var kitLocations = new List<PortalKitLocationResponse>();
        var orderResponses = new List<PortalOrderResponse>();
        var customerFaults = await repository.GetFaultTicketsAsync(customerId, cancellationToken);
        var customerReturns = await repository.GetKitReturnRequestsAsync(customerId, cancellationToken);
        var rentalCohorts = await repository.GetRentalCohortsAsync(customerId, cancellationToken);
        var linkedStudents = rentalCohorts
            .SelectMany(cohort => cohort.Students
                .Where(student => !student.IsDeleted &&
                    (student.AssignmentId.HasValue || student.ProductUnitId.HasValue))
                .Select(student => new
                {
                    student.AssignmentId,
                    student.ProductUnitId,
                    student.FullName,
                    student.GuardianPhone,
                    student.AddressLine,
                    CohortName = cohort.Name
                }))
            .ToArray();
        var studentsByAssignmentId = linkedStudents
            .Where(student => student.AssignmentId.HasValue)
            .GroupBy(student => student.AssignmentId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var studentsByProductUnitId = linkedStudents
            .Where(student => student.ProductUnitId.HasValue)
            .GroupBy(student => student.ProductUnitId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var kitLocationEvents = await repository.GetKitLocationEventsAsync(cancellationToken);
        var latestLocationsByUnit = kitLocationEvents
            .Where(location => location.CustomerId == customerId)
            .GroupBy(location => location.ProductUnitId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(location => location.OccurredAt)
                .ThenByDescending(location => location.Id).First());
        var deliveryFormAssignmentIds = kitLocationEvents
            .Where(location => location.CustomerId == customerId &&
                location.Source == KitLocationEventSource.DeliveryReceipt &&
                location.AssignmentId.HasValue)
            .Select(location => location.AssignmentId!.Value)
            .ToHashSet();
        var today = TurkeyTime.Today();
        var returnProcessStartedAssignmentIds = customerReturns
            .Where(item => item.Status is KitReturnStatus.Requested or KitReturnStatus.InTransit)
            .SelectMany(item => item.Items)
            .Select(item => item.AssignmentId)
            .ToHashSet();
        var returnedAssignmentIds = customerReturns
            .Where(item => item.Status == KitReturnStatus.Received)
            .SelectMany(item => item.Items)
            .Select(item => item.AssignmentId)
            .ToHashSet();
        foreach (var order in orders.Where(item => item.Type == OrderType.Rental))
        {
            orderResponses.Add(new PortalOrderResponse(order.Id, order.OrderNumber, customer.Id, customer.Name,
                order.Type, order.Status, order.Period!.Value.StartDate, order.Period.Value.EndDate, order.CreatedAt,
                order.Lines.Select(line => new PortalOrderLineResponse(line.ProductModelId,
                    modelLookup.TryGetValue(line.ProductModelId, out var lineModel) ? lineModel.Name : "Eğitim kiti",
                    modelLookup.TryGetValue(line.ProductModelId, out lineModel) ? lineModel.Sku : "-", line.Quantity)).ToArray()));

            var lineIds = order.Lines.Select(line => line.Id).ToHashSet();
            foreach (var assignment in await repository.GetAssignmentsForOrderAsync(order.Id, cancellationToken))
            {
                if (!lineIds.Contains(assignment.OrderLineId) || assignment.Status == RentalAssignmentStatus.Cancelled)
                    continue;
                var unit = await repository.GetProductUnitAsync(assignment.ProductUnitId, cancellationToken);
                if (unit is null || !modelLookup.TryGetValue(unit.ProductModelId, out var model))
                    continue;
                var openFaults = customerFaults.Count(ticket =>
                    ticket.ProductUnitId == unit.Id && ticket.Status is not (FaultStatus.Resolved or FaultStatus.Closed));
                var linkedStudent = studentsByAssignmentId.TryGetValue(assignment.Id, out var studentByAssignment)
                    ? studentByAssignment
                    : studentsByProductUnitId.TryGetValue(unit.Id, out var studentByUnit)
                        ? studentByUnit
                        : null;
                kits.Add(new PortalKitResponse(unit.Id, assignment.Id, order.Id, order.OrderNumber, model.Name, model.Sku,
                    model.ImageUrl, unit.SerialNumber, unit.QrCode, unit.Status, assignment.Status, assignment.Period.StartDate,
                    assignment.Period.EndDate, openFaults, deliveryFormAssignmentIds.Contains(assignment.Id),
                    linkedStudent?.FullName, linkedStudent?.GuardianPhone, linkedStudent?.AddressLine,
                    linkedStudent?.CohortName, returnedAssignmentIds.Contains(assignment.Id)));
                if (assignment.Status == RentalAssignmentStatus.Active &&
                    !returnedAssignmentIds.Contains(assignment.Id))
                {
                    var locationCategory = GetKitLocationCategory(unit.Status, openFaults > 0,
                        returnProcessStartedAssignmentIds.Contains(assignment.Id), assignment.Period.EndDate < today);
                    if (latestLocationsByUnit.TryGetValue(unit.Id, out var location))
                    {
                        kitLocations.Add(new PortalKitLocationResponse(unit.Id, unit.ProductModelId, model.Name,
                            model.Sku, unit.SerialNumber,
                            location.ContactName, location.AddressLine, location.District, location.City,
                            (int)unit.Status, location.Latitude, location.Longitude, locationCategory));
                    }
                    else
                    {
                        var address = order.DeliveryAddress;
                        kitLocations.Add(new PortalKitLocationResponse(unit.Id, unit.ProductModelId, model.Name,
                            model.Sku, unit.SerialNumber,
                            address.ContactName, address.Line1, address.District, address.City, (int)unit.Status,
                            null, null, locationCategory));
                    }
                }
            }
        }

        var faults = await MapFaultsAsync(customerId, modelLookup, cancellationToken);
        var returns = await MapReturnsAsync(customerId, cancellationToken);
        var expiredRentalKitCount = kits.Count(item =>
            item.AssignmentStatus == RentalAssignmentStatus.Active &&
            item.EndDate < today &&
            !returnProcessStartedAssignmentIds.Contains(item.AssignmentId));
        var currentlyRentedKits = kits
            .Where(item => item.AssignmentStatus is RentalAssignmentStatus.Reserved or RentalAssignmentStatus.Active &&
                !returnedAssignmentIds.Contains(item.AssignmentId))
            .ToArray();
        var assignedStudentKitCount = currentlyRentedKits.Count(item =>
            !string.IsNullOrWhiteSpace(item.AssignedStudentName));
        var unassignedKitCount = currentlyRentedKits.Count(item =>
            string.IsNullOrWhiteSpace(item.AssignedStudentName));
        var undeliveredKitCount = kits.Count(item =>
            item.AssignmentStatus is RentalAssignmentStatus.Reserved or RentalAssignmentStatus.Active &&
            !item.HasDeliveryForm);
        return new CustomerPortalResponse(customer.Name, customer.Email,
            kits.Count,
            undeliveredKitCount,
            assignedStudentKitCount,
            unassignedKitCount,
            orders.Count(item => item.Status == RentalOrderStatus.PendingApproval),
            faults.Count(item => item.Status is not (FaultStatus.Resolved or FaultStatus.Closed)),
            faults.Count(item => item.Status is FaultStatus.Resolved or FaultStatus.Closed),
            expiredRentalKitCount,
            returnProcessStartedAssignmentIds.Count,
            returnedAssignmentIds.Count,
            kits.OrderByDescending(item => item.AssignmentStatus).ThenBy(item => item.KitName).ToArray(),
            orderResponses, faults,
            customer.Addresses.Select(item => new PortalAddressResponse(item.Id, item.Title, item.ContactName, item.Phone,
                item.Line1, item.District, item.City, item.PostalCode)).ToArray(),
            productModels.Select(item => new PortalProductModelResponse(item.Id, item.Name, item.Sku, item.Description,
                item.ImageUrl)).ToArray(), returns,
            kitLocations.OrderBy(item => item.City).ThenBy(item => item.District).ThenBy(item => item.SerialNumber).ToArray(),
            await MapRentalCohortsAsync(customerId, cancellationToken));
    }

    private static string GetKitLocationCategory(ProductUnitStatus status, bool hasOpenFault,
        bool hasReturnProcessStarted, bool isExpired) =>
        hasOpenFault
            ? "faulty"
            : hasReturnProcessStarted
                ? "returning"
                : isExpired
                    ? "expired"
                    : "active";

    public Task<IReadOnlyCollection<PortalKitReturnResponse>> GetReturnsAsync(Guid? customerId,
        CancellationToken cancellationToken) => MapReturnsAsync(customerId, cancellationToken);

    public Task<IReadOnlyCollection<PortalRentalCohortResponse>> GetRentalCohortsAsync(Guid customerId,
        CancellationToken cancellationToken) => MapRentalCohortsAsync(customerId, cancellationToken);

    public async Task<RentalOrder> CreateRentalCohortOrderAsync(CreatePortalRentalCohortOrderCommand command,
        CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(command.CustomerId, cancellationToken)
            ?? throw new ResourceNotFoundException("Müşteri hesabı bulunamadı.");
        var address = customer.Addresses.FirstOrDefault()
            ?? throw new ConflictException("customer.address_required", "Sipariş oluşturmak için müşteri teslimat adresi bulunmalıdır.");
        var cohort = await GetOwnedCohortAsync(command.CustomerId, command.CohortId, cancellationToken);
        var students = cohort.Students.Where(item => !item.IsDeleted).ToArray();
        if (students.Length == 0)
            throw new ConflictException("rental_cohort.no_students", "Sipariş oluşturmak için öğrenci listesi boş olmamalıdır.");
        if (students.Any(item => item.OrderId.HasValue))
            throw new ConflictException("rental_cohort.order_already_created", "Bu dönem için daha önce sipariş oluşturulmuş.");

        var lines = students.GroupBy(item => item.ProductModelId)
            .Select(group => new OrderLineCommand(group.Key, group.Count()))
            .ToArray();
        var order = await operationsService.CreateOrderAsync(new CreateOrderCommand(command.CustomerId,
            address.Id, cohort.StartDate, cohort.EndDate, lines, command.ActorId), cancellationToken);
        cohort.LinkActiveStudentsToOrder(order.Id);
        var now = TurkeyTime.Now();
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), command.ActorId, nameof(RentalCohort),
            cohort.Id, "OrderCreated", null, order.OrderNumber, now), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return order;
    }

    public async Task<PortalRentalCohortResponse> SaveRentalCohortAsync(SaveRentalCohortCommand command,
        CancellationToken cancellationToken)
    {
        _ = await repository.GetCustomerAsync(command.CustomerId, cancellationToken)
            ?? throw new ResourceNotFoundException("Müşteri hesabı bulunamadı.");
        RentalCohort cohort;
        var action = "RentalCohortCreated";
        if (command.Id.HasValue)
        {
            cohort = await GetOwnedCohortAsync(command.CustomerId, command.Id.Value, cancellationToken);
            await EnsureCohortPlanEditableAsync(cohort, cancellationToken);
            cohort.Update(command.Name, command.StartDate, command.EndDate);
            await SyncLinkedUnapprovedOrderPlanAsync(cohort, cancellationToken);
            action = "RentalCohortUpdated";
        }
        else
        {
            cohort = RentalCohort.Create(Guid.NewGuid(), command.CustomerId, command.Name,
                command.StartDate, command.EndDate, TurkeyTime.Now());
            await repository.AddRentalCohortAsync(cohort, cancellationToken);
        }
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), command.ActorId, nameof(RentalCohort),
            cohort.Id, action, null, cohort.Name, TurkeyTime.Now()), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await MapRentalCohortAsync(cohort, cancellationToken);
    }

    public async Task DeleteRentalCohortAsync(DeleteRentalCohortCommand command,
        CancellationToken cancellationToken)
    {
        var cohort = await GetOwnedCohortAsync(command.CustomerId, command.CohortId, cancellationToken);
        await EnsureCohortPlanEditableAsync(cohort, cancellationToken);
        var linkedOrder = await GetSingleLinkedOrderAsync(cohort, cancellationToken);
        if (cohort.Students.Any(item => !item.IsDeleted && (item.AssignmentId.HasValue || item.ProductUnitId.HasValue)))
            throw new ConflictException("rental_cohort.delete_locked",
                "Kit ataması yapılan sipariş dönemleri silinemez.");

        await repository.RemoveRentalCohortAsync(cohort, cancellationToken);
        if (linkedOrder is not null)
            await repository.RemoveOrderAsync(linkedOrder, cancellationToken);
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), command.ActorId, nameof(RentalCohort),
            cohort.Id, "RentalCohortDeleted", cohort.Name, linkedOrder?.OrderNumber, TurkeyTime.Now()),
            cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<PortalRentalCohortStudentResponse> SaveRentalCohortStudentAsync(
        SaveRentalCohortStudentCommand command, CancellationToken cancellationToken)
    {
        var cohort = await GetOwnedCohortAsync(command.CustomerId, command.CohortId, cancellationToken);
        await EnsureCohortStudentsEditableAsync(cohort, cancellationToken);
        _ = await repository.GetProductModelAsync(command.ProductModelId, cancellationToken)
            ?? throw new ResourceNotFoundException("Eğitim kiti bulunamadı.");
        var student = command.Id.HasValue
            ? cohort.UpdateStudent(command.Id.Value, command.FullName, command.GuardianPhone, command.AddressLine,
                command.CityId, command.DistrictId, command.City, command.District, command.ProductModelId)
            : cohort.AddStudent(command.FullName, command.GuardianPhone, command.AddressLine, command.CityId,
                command.DistrictId, command.City, command.District, command.ProductModelId);
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), command.ActorId, nameof(RentalCohort),
            cohort.Id, command.Id.HasValue ? "StudentUpdated" : "StudentAdded", null, student.FullName,
            TurkeyTime.Now()), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return (await MapRentalCohortAsync(cohort, cancellationToken)).Students.Single(item => item.Id == student.Id);
    }

    public async Task<PortalRentalCohortResponse> ImportRentalCohortStudentsAsync(Guid customerId, Guid cohortId,
        IReadOnlyCollection<ImportRentalCohortStudentCommand> rows, Guid actorId, string actorDisplayName,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            throw new ConflictException("rental_cohort.import_empty", "İçe aktarılacak öğrenci satırı bulunamadı.");
        var cohort = await GetOwnedCohortAsync(customerId, cohortId, cancellationToken);
        await EnsureCohortStudentsEditableAsync(cohort, cancellationToken);
        var models = await repository.GetProductModelsAsync(cancellationToken);
        foreach (var row in rows)
        {
            var model = FindModel(models, row.ProductModel)
                ?? throw new ResourceNotFoundException($"{row.ProductModel} eğitim kiti bulunamadı.");
            cohort.AddStudent(row.FullName, row.GuardianPhone, row.AddressLine, row.CityId, row.DistrictId,
                row.City, row.District, model.Id);
        }
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), actorId, nameof(RentalCohort),
            cohort.Id, "StudentsImported", null, $"{rows.Count} öğrenci", TurkeyTime.Now()), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await MapRentalCohortAsync(cohort, cancellationToken);
    }

    public async Task RemoveRentalCohortStudentAsync(Guid customerId, Guid cohortId, Guid studentId,
        Guid actorId, string actorDisplayName, CancellationToken cancellationToken)
    {
        var cohort = await GetOwnedCohortAsync(customerId, cohortId, cancellationToken);
        await EnsureCohortStudentsEditableAsync(cohort, cancellationToken);
        var student = cohort.Students.SingleOrDefault(item => item.Id == studentId && !item.IsDeleted)
            ?? throw new ResourceNotFoundException("Öğrenci bulunamadı.");
        var studentName = student.FullName;
        var unitId = student.ProductUnitId;
        var assignmentId = student.AssignmentId;
        var orderId = student.OrderId;
        cohort.RemoveStudent(studentId);
        if (unitId.HasValue)
            await AddActivityAsync(unitId.Value, assignmentId, orderId, studentId, actorId, actorDisplayName,
                "Öğrenci ataması kaldırıldı", $"{studentName} öğrencisi kit üzerinden kaldırıldı.",
                cancellationToken);
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), actorId, nameof(RentalCohort),
            cohort.Id, "StudentRemoved", studentName, unitId?.ToString(), TurkeyTime.Now()), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<KitReturnRequest> CreatePortalStudentReturnAsync(CreatePortalStudentReturnCommand command,
        CancellationToken cancellationToken)
    {
        var cohort = await GetOwnedCohortAsync(command.CustomerId, command.CohortId, cancellationToken);
        var student = cohort.Students.SingleOrDefault(item => item.Id == command.StudentId && !item.IsDeleted)
            ?? throw new ResourceNotFoundException("Öğrenci bulunamadı.");
        if (!student.AssignmentId.HasValue || !student.ProductUnitId.HasValue || !student.OrderId.HasValue)
            throw new ConflictException("kit_return.student_not_assigned", "Bu öğrenciye atanmış bir kit yok.");
        var activeReturns = await repository.GetKitReturnRequestsAsync(command.CustomerId, cancellationToken);
        if (activeReturns.Where(item => item.Status == KitReturnStatus.Received)
            .SelectMany(item => item.Items)
            .Any(item => item.AssignmentId == student.AssignmentId.Value))
            throw new ConflictException("kit_return.already_received", "İade edilmiş kit üzerinde işlem yapılamaz.");
        if (activeReturns.Where(item => item.Status != KitReturnStatus.Received)
            .SelectMany(item => item.Items)
            .Any(item => item.AssignmentId == student.AssignmentId.Value))
            throw new ConflictException("kit_return.already_started", "Bu kit için iade süreci zaten devam ediyor.");
        var now = TurkeyTime.Now();
        var request = KitReturnRequest.CreatePublic(Guid.NewGuid(), command.CustomerId, now, command.ActorId,
            [new KitReturnItem(Guid.NewGuid(), student.AssignmentId.Value, student.ProductUnitId.Value,
                student.OrderId.Value)],
            student.FullName, student.GuardianPhone, student.AddressLine, null, null);
        await repository.AddKitReturnRequestAsync(request, cancellationToken);
        await AddActivityAsync(student.ProductUnitId.Value, student.AssignmentId, student.OrderId, student.Id,
            command.ActorId, command.ActorDisplayName, "İade talebi oluşturuldu",
            $"{student.FullName} öğrencisi iade talebi oluşturdu.", cancellationToken, now);
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), command.ActorId,
            nameof(KitReturnRequest), request.Id, "StudentReturnRequested", null,
            student.FullName, now), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<KitReturnRequest> CreatePortalReturnAsync(CreatePortalReturnCommand command,
        CancellationToken cancellationToken)
    {
        if (command.AssignmentIds.Count == 0)
            throw new DomainException("kit_return.required_fields", "İade için en az bir kit seçilmelidir.");
        var requestedAssignmentIds = command.AssignmentIds.Distinct().ToArray();
        if (requestedAssignmentIds.Length != command.AssignmentIds.Count)
            throw new ConflictException("kit_return.duplicate_assignment", "Aynı kit bir iadeye birden fazla eklenemez.");

        var activeReturns = await repository.GetKitReturnRequestsAsync(command.CustomerId, cancellationToken);
        var returnedAssignmentIds = activeReturns.Where(item => item.Status == KitReturnStatus.Received)
            .SelectMany(item => item.Items)
            .Select(item => item.AssignmentId)
            .ToHashSet();
        var activeReturnAssignmentIds = activeReturns.Where(item => item.Status != KitReturnStatus.Received)
            .SelectMany(item => item.Items)
            .Select(item => item.AssignmentId)
            .ToHashSet();
        var items = new List<KitReturnItem>();
        foreach (var assignmentId in requestedAssignmentIds)
        {
            var assignment = await repository.GetRentalAssignmentAsync(assignmentId, cancellationToken)
                ?? throw new ResourceNotFoundException("Kiralama ataması bulunamadı.");
            if (assignment.CustomerId != command.CustomerId)
                throw new ForbiddenException("Başka bir müşterinin kitini iade edemezsiniz.");
            if (returnedAssignmentIds.Contains(assignment.Id))
                throw new ConflictException("kit_return.already_received", "İade edilmiş kit üzerinde işlem yapılamaz.");
            if (assignment.Status != RentalAssignmentStatus.Active)
                throw new ConflictException("kit_return.assignment_not_active", "Yalnızca aktif kiralamadaki kitler iade edilebilir.");
            if (activeReturnAssignmentIds.Contains(assignment.Id))
                throw new ConflictException("kit_return.already_started", "Bu kit için iade süreci zaten devam ediyor.");
            var order = await repository.FindOrderByLineIdAsync(assignment.OrderLineId, cancellationToken)
                ?? throw new ResourceNotFoundException("Kiralama siparişi bulunamadı.");
            _ = await repository.GetProductUnitAsync(assignment.ProductUnitId, cancellationToken)
                ?? throw new ResourceNotFoundException("Fiziksel kit bulunamadı.");
            items.Add(new KitReturnItem(Guid.NewGuid(), assignment.Id, assignment.ProductUnitId, order.Id));
        }

        var now = TurkeyTime.Now();
        var request = KitReturnRequest.Create(Guid.NewGuid(), command.CustomerId, now, command.ActorId, items);
        await repository.AddKitReturnRequestAsync(request, cancellationToken);
        foreach (var item in request.Items)
            await AddActivityAsync(item.ProductUnitId, item.AssignmentId, item.OrderId, null, command.ActorId,
                command.ActorDisplayName, "İade talebi oluşturuldu",
                "Müşteri portalından iade talebi oluşturuldu.", cancellationToken, now);
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), command.ActorId,
            nameof(KitReturnRequest), request.Id, "ReturnRequested", null,
            $"{request.Items.Count} kit", now), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<KitReturnRequest> ShipPortalReturnAsync(ShipPortalReturnCommand command,
        CancellationToken cancellationToken)
    {
        var request = await repository.GetKitReturnRequestAsync(command.ReturnId, cancellationToken)
            ?? throw new ResourceNotFoundException("İade kaydı bulunamadı.");
        if (request.CustomerId != command.CustomerId)
            throw new ForbiddenException("Başka bir müşterinin iade kaydına erişemezsiniz.");
        var now = TurkeyTime.Now();
        request.MarkShipped(command.Carrier, command.TrackingNumber, now);
        foreach (var item in request.Items)
        {
            var unit = await repository.GetProductUnitAsync(item.ProductUnitId, cancellationToken)
                ?? throw new ResourceNotFoundException("Fiziksel kit bulunamadı.");
            if (unit.Status == ProductUnitStatus.WithCustomer)
                unit.StartReturn(command.ActorId, now);
            await AddActivityAsync(item.ProductUnitId, item.AssignmentId, item.OrderId, null,
                command.ActorId, command.ActorDisplayName, "İade kargoya verildi",
                $"{command.Carrier.Trim()} takip numarası {command.TrackingNumber.Trim()} ile iade kargoya verildi.",
                cancellationToken, now);
        }
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), command.ActorId,
            nameof(KitReturnRequest), request.Id, "ReturnShipped", null,
            request.TrackingNumber, now), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<KitReturnRequest> CreatePublicKitReturnAsync(CreatePublicKitReturnCommand command,
        CancellationToken cancellationToken)
    {
        if (!command.ReturnReason.HasValue)
            throw new DomainException("kit_return.reason_required", "İade nedeni seçilmelidir.");
        var unit = (await repository.GetProductUnitsAsync(cancellationToken))
            .SingleOrDefault(item => string.Equals(item.QrCode, command.QrCode.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ResourceNotFoundException("Bu QR kodla eşleşen fiziksel kit bulunamadı.");
        if (unit.Status != ProductUnitStatus.WithCustomer)
            throw new ConflictException("kit_return.unit_not_with_customer", "Bu kit şu anda müşteride görünmüyor.");
        var assignment = (await repository.GetAssignmentsForProductUnitAsync(unit.Id, cancellationToken))
            .Where(item => item.Status == RentalAssignmentStatus.Active)
            .OrderByDescending(item => item.Period.EndDate)
            .FirstOrDefault()
            ?? throw new ConflictException("kit_return.no_active_rental", "Bu kit için aktif bir kiralama bulunmuyor.");
        var activeReturns = await repository.GetKitReturnRequestsAsync(assignment.CustomerId, cancellationToken);
        if (activeReturns.Where(item => item.Status != KitReturnStatus.Received)
            .SelectMany(item => item.Items)
            .Any(item => item.AssignmentId == assignment.Id))
            throw new ConflictException("kit_return.already_started", "Bu kit için iade süreci zaten devam ediyor.");
        var order = await repository.FindOrderByLineIdAsync(assignment.OrderLineId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kiralama siparişi bulunamadı.");
        var now = TurkeyTime.Now();
        var resolvedLocation = ResolveLocation(command.District, command.City,
            command.Latitude, command.Longitude);
        var request = KitReturnRequest.CreatePublic(Guid.NewGuid(), assignment.CustomerId, now, PublicActorId,
            [new KitReturnItem(Guid.NewGuid(), assignment.Id, assignment.ProductUnitId, order.Id)],
            command.RequesterName, command.RequesterPhone,
            command.ReturnAddress, resolvedLocation.Latitude, resolvedLocation.Longitude, command.ReturnReason);
        await repository.AddKitReturnRequestAsync(request, cancellationToken);
        await repository.AddKitLocationEventAsync(KitLocationEvent.Create(Guid.NewGuid(), unit.Id, assignment.Id,
            order.Id, assignment.CustomerId, KitLocationEventSource.ReturnRequest, request.Id,
            command.RequesterName, command.RequesterPhone, command.ReturnAddress, resolvedLocation.District,
            resolvedLocation.City, resolvedLocation.Latitude, resolvedLocation.Longitude, now, PublicActorId),
            cancellationToken);
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), PublicActorId,
            nameof(KitReturnRequest), request.Id, "PublicReturnRequested", null,
            command.RequesterName.Trim(), now), cancellationToken);
        await AddActivityAsync(unit.Id, assignment.Id, order.Id, null, PublicActorId, command.RequesterName,
            "İade talebi oluşturuldu", $"{command.RequesterName.Trim()} iade talebi oluşturdu.",
            cancellationToken, now);
        await repository.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<KitReturnRequest> ReceiveKitReturnAsync(Guid returnId, Guid actorId,
        CancellationToken cancellationToken)
    {
        var request = await repository.GetKitReturnRequestAsync(returnId, cancellationToken)
            ?? throw new ResourceNotFoundException("İade kaydı bulunamadı.");
        var now = TurkeyTime.Now();
        request.Receive(now);
        var customerCohorts = await repository.GetRentalCohortsAsync(request.CustomerId, cancellationToken);
        foreach (var item in request.Items)
        {
            var unit = await repository.GetProductUnitAsync(item.ProductUnitId, cancellationToken)
                ?? throw new ResourceNotFoundException("Fiziksel kit bulunamadı.");
            if (unit.Status == ProductUnitStatus.WithCustomer)
                unit.StartReturn(actorId, now);
            unit.ReceiveReturnToAvailable(actorId, now);
            var assignment = await repository.GetRentalAssignmentAsync(item.AssignmentId, cancellationToken);
            if (assignment?.Status == RentalAssignmentStatus.Active) assignment.Complete();
            var cohort = customerCohorts.FirstOrDefault(candidate =>
                candidate.Students.Any(student => student.AssignmentId == item.AssignmentId && !student.IsDeleted));
            var student = cohort?.UnlinkStudentKit(item.AssignmentId);
            if (student is not null && cohort is not null)
            {
                await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), actorId,
                    nameof(RentalCohort), cohort.Id, "StudentKitUnlinkedByReturn", student.FullName,
                    unit.SerialNumber, now), cancellationToken);
            }
            var studentDescription = student is null
                ? "İade teslim alındı; kit yeniden kullanılabilir stoka alındı."
                : $"İade teslim alındı; {student.FullName} öğrencisinin kit ilişkisi kaldırıldı.";
            await AddActivityAsync(unit.Id, item.AssignmentId, item.OrderId, student?.Id, actorId, actorId.ToString(),
                "İade teslim alındı", studentDescription,
                cancellationToken, now);
        }
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), actorId,
            nameof(KitReturnRequest), request.Id, "ReturnReceived", null,
            $"{request.Items.Count} kit", now), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return request;
    }

    private async Task<IReadOnlyCollection<PortalKitReturnResponse>> MapReturnsAsync(Guid? customerId,
        CancellationToken cancellationToken)
    {
        var customers = (await repository.GetCustomersAsync(cancellationToken)).ToDictionary(x => x.Id);
        var models = (await repository.GetProductModelsAsync(cancellationToken)).ToDictionary(x => x.Id);
        var result = new List<PortalKitReturnResponse>();
        foreach (var request in await repository.GetKitReturnRequestsAsync(customerId, cancellationToken))
        {
            var items = new List<PortalKitReturnItemResponse>();
            foreach (var item in request.Items)
            {
                var unit = await repository.GetProductUnitAsync(item.ProductUnitId, cancellationToken);
                items.Add(new PortalKitReturnItemResponse(item.AssignmentId, item.ProductUnitId, item.OrderId,
                    unit is not null && models.TryGetValue(unit.ProductModelId, out var model) ? model.Name : "Eğitim kiti",
                    unit?.SerialNumber ?? "-"));
            }
            result.Add(new PortalKitReturnResponse(request.Id, request.CustomerId,
                customers.TryGetValue(request.CustomerId, out var customer) ? customer.Name : "Müşteri",
                request.Status, request.Carrier, request.TrackingNumber, request.CreatedAt, request.ShippedAt,
                request.RequesterName, request.RequesterPhone,
                request.ReturnAddress, request.Latitude, request.Longitude, items));
        }
        return result;
    }

    public async Task<IReadOnlyCollection<PortalOrderResponse>> GetOrderSummariesAsync(Guid? customerId,
        CancellationToken cancellationToken)
    {
        var customers = (await repository.GetCustomersAsync(cancellationToken)).ToDictionary(item => item.Id);
        var models = (await repository.GetProductModelsAsync(cancellationToken)).ToDictionary(item => item.Id);
        var result = new List<PortalOrderResponse>();
        foreach (var order in await repository.GetOrdersAsync(customerId, cancellationToken))
        {
            var assignedKitCount = order.Type == OrderType.Rental
                ? (await repository.GetAssignmentsForOrderAsync(order.Id, cancellationToken)).Count
                : order.ProductUnits.Count;
            result.Add(new PortalOrderResponse(order.Id, order.OrderNumber, order.CustomerId,
                customers.TryGetValue(order.CustomerId, out var customer) ? customer.Name : "Müşteri",
                order.Type, order.Status, order.Period?.StartDate, order.Period?.EndDate, order.CreatedAt,
                order.Lines.Select(line => new PortalOrderLineResponse(line.ProductModelId,
                    models.TryGetValue(line.ProductModelId, out var model) ? model.Name : "Eğitim kiti",
                    models.TryGetValue(line.ProductModelId, out model) ? model.Sku : "-", line.Quantity)).ToArray(),
                assignedKitCount));
        }
        return result;
    }

    public async Task<RentalOrder> ConfirmOrderDeliveryAsync(ConfirmPortalOrderDeliveryCommand command,
        CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(command.OrderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Sipariş bulunamadı.");
        if (order.CustomerId != command.CustomerId)
            throw new ForbiddenException("Yalnızca hesabınıza ait siparişlerin teslimatını onaylayabilirsiniz.");
        if (order.Type != OrderType.Rental)
            throw new ForbiddenException("Satın alma siparişleri müşteri portalından yönetilemez.");
        if (order.Status != RentalOrderStatus.OutboundInTransit)
            throw new ConflictException("order.delivery_confirmation_not_allowed",
                "Yalnızca kargoya verilmiş siparişler teslim alındı olarak işaretlenebilir.");

        return await operationsService.TransitionOrderAsync(order.Id, RentalOrderStatus.Delivered,
            command.ActorId, cancellationToken);
    }

    public async Task<FaultTicket> OpenFaultAsync(OpenPortalFaultCommand command, CancellationToken cancellationToken)
    {
        var assignment = await repository.GetRentalAssignmentAsync(command.AssignmentId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kiralama kaydı bulunamadı.");
        if (assignment.CustomerId != command.CustomerId || assignment.Status != RentalAssignmentStatus.Active)
            throw new ForbiddenException("Yalnızca hesabınıza ait aktif kiralamalar için arıza kaydı açabilirsiniz.");
        var customerReturns = await repository.GetKitReturnRequestsAsync(command.CustomerId, cancellationToken);
        if (customerReturns.Where(item => item.Status == KitReturnStatus.Received)
            .SelectMany(item => item.Items)
            .Any(item => item.AssignmentId == assignment.Id))
            throw new ConflictException("fault.returned_kit_readonly", "İade edilmiş kit üzerinde arıza kaydı açılamaz.");
        var order = await repository.FindOrderByLineIdAsync(assignment.OrderLineId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kiralama siparişi bulunamadı.");
        var student = (await repository.GetRentalCohortsAsync(command.CustomerId, cancellationToken))
            .SelectMany(item => item.Students)
            .FirstOrDefault(item => item.AssignmentId == assignment.Id && !item.IsDeleted);
        var ticket = await operationsService.OpenFaultAsync(new OpenFaultCommand(command.CustomerId, order.Id, assignment.Id,
            assignment.ProductUnitId, command.Category, command.Severity, command.Description, command.ActorId,
            student?.FullName, student?.GuardianPhone, student?.AddressLine),
            cancellationToken);
        return ticket;
    }

    private async Task<RentalCohort> GetOwnedCohortAsync(Guid customerId, Guid cohortId,
        CancellationToken cancellationToken)
    {
        var cohort = await repository.GetRentalCohortAsync(cohortId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kiralama dönemi bulunamadı.");
        if (cohort.CustomerId != customerId)
            throw new ForbiddenException("Başka bir müşterinin kiralama dönemine erişemezsiniz.");
        return cohort;
    }

    private async Task EnsureCohortStudentsEditableAsync(RentalCohort cohort, CancellationToken cancellationToken)
    {
        var linkedOrderIds = cohort.Students
            .Where(item => !item.IsDeleted && item.OrderId.HasValue)
            .Select(item => item.OrderId!.Value)
            .Distinct()
            .ToArray();
        foreach (var orderId in linkedOrderIds)
        {
            var order = await repository.GetOrderAsync(orderId, cancellationToken);
            if (order is not null && IsApprovedOrderStatus(order.Status))
                throw new ConflictException("rental_cohort.students_locked",
                    "Onaylanmış kiralama dönemlerinde öğrenci ekleme, güncelleme veya silme yapılamaz.");
        }
    }

    private async Task EnsureCohortPlanEditableAsync(RentalCohort cohort, CancellationToken cancellationToken)
    {
        var linkedOrder = await GetSingleLinkedOrderAsync(cohort, cancellationToken);
        if (linkedOrder is not null && IsApprovedOrderStatus(linkedOrder.Status))
            throw new ConflictException("rental_cohort.plan_locked",
                "Onaylanmış siparişlerde dönem adı veya kiralama tarih aralığı düzenlenemez.");
    }

    private async Task SyncLinkedUnapprovedOrderPlanAsync(RentalCohort cohort, CancellationToken cancellationToken)
    {
        var linkedOrder = await GetSingleLinkedOrderAsync(cohort, cancellationToken);
        if (linkedOrder is null) return;
        var lines = cohort.Students
            .Where(item => !item.IsDeleted)
            .GroupBy(item => item.ProductModelId)
            .Select(group => (group.Key, group.Count()))
            .ToArray();
        linkedOrder.UpdateUnapprovedRentalPlan(cohort.StartDate, cohort.EndDate, lines);
    }

    private async Task<RentalOrder?> GetSingleLinkedOrderAsync(RentalCohort cohort,
        CancellationToken cancellationToken)
    {
        var linkedOrderIds = cohort.Students
            .Where(item => !item.IsDeleted && item.OrderId.HasValue)
            .Select(item => item.OrderId!.Value)
            .Distinct()
            .ToArray();
        if (linkedOrderIds.Length == 0) return null;
        if (linkedOrderIds.Length > 1)
            throw new ConflictException("rental_cohort.multiple_orders",
                "Bu sipariş dönemi birden fazla siparişe bağlı olduğu için düzenlenemez.");
        return await repository.GetOrderAsync(linkedOrderIds[0], cancellationToken)
            ?? throw new ResourceNotFoundException("Kiralama siparişi bulunamadı.");
    }

    private async Task<IReadOnlyCollection<PortalRentalCohortResponse>> MapRentalCohortsAsync(Guid customerId,
        CancellationToken cancellationToken)
    {
        var result = new List<PortalRentalCohortResponse>();
        foreach (var cohort in await repository.GetRentalCohortsAsync(customerId, cancellationToken))
            result.Add(await MapRentalCohortAsync(cohort, cancellationToken));
        return result;
    }

    private async Task<PortalRentalCohortResponse> MapRentalCohortAsync(RentalCohort cohort,
        CancellationToken cancellationToken)
    {
        var models = (await repository.GetProductModelsAsync(cancellationToken)).ToDictionary(item => item.Id);
        var returns = await repository.GetKitReturnRequestsAsync(cohort.CustomerId, cancellationToken);
        var activeReturnAssignmentIds = returns.Where(item => item.Status != KitReturnStatus.Received)
            .SelectMany(item => item.Items)
            .Select(item => item.AssignmentId)
            .ToHashSet();
        var deliveryEventsByAssignment = (await repository.GetKitLocationEventsAsync(cancellationToken))
            .Where(item => item.CustomerId == cohort.CustomerId &&
                item.Source == KitLocationEventSource.DeliveryReceipt &&
                item.AssignmentId.HasValue)
            .GroupBy(item => item.AssignmentId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.OccurredAt)
                .ThenByDescending(item => item.Id).First());
        var students = new List<PortalRentalCohortStudentResponse>();
        foreach (var student in cohort.Students.Where(item => !item.IsDeleted))
        {
            models.TryGetValue(student.ProductModelId, out var model);
            ProductUnit? unit = student.ProductUnitId.HasValue
                ? await repository.GetProductUnitAsync(student.ProductUnitId.Value, cancellationToken)
                : null;
            var delivery = student.AssignmentId.HasValue &&
                deliveryEventsByAssignment.TryGetValue(student.AssignmentId.Value, out var foundDelivery)
                    ? foundDelivery
                    : null;
            students.Add(new PortalRentalCohortStudentResponse(student.Id, student.FullName, student.GuardianPhone,
                student.AddressLine, student.CityId, student.DistrictId, student.City, student.District, student.ProductModelId, model?.Name ?? "Eğitim kiti", model?.Sku ?? "-",
                student.OrderId, student.AssignmentId, student.ProductUnitId, unit?.SerialNumber, unit?.QrCode,
                student.IsDeleted, student.AssignmentId.HasValue &&
                    activeReturnAssignmentIds.Contains(student.AssignmentId.Value),
                delivery is not null, delivery?.ContactName, delivery?.ContactPhone, delivery?.AddressLine,
                delivery?.District, delivery?.City, delivery?.OccurredAt));
        }
        var assignedStudentUnitIds = cohort.Students.Where(item => !item.IsDeleted && item.ProductUnitId.HasValue)
            .Select(item => item.ProductUnitId!.Value)
            .ToHashSet();
        var unassigned = new List<PortalUnassignedCohortKitResponse>();
        foreach (var deleted in cohort.Students.Where(item => item.IsDeleted && item.ProductUnitId.HasValue))
        {
            if (!deleted.ProductUnitId.HasValue) continue;
            var unit = await repository.GetProductUnitAsync(deleted.ProductUnitId.Value, cancellationToken);
            if (unit is null || assignedStudentUnitIds.Contains(unit.Id)) continue;
            models.TryGetValue(unit.ProductModelId, out var model);
            unassigned.Add(new PortalUnassignedCohortKitResponse(unit.Id, deleted.AssignmentId ?? Guid.Empty,
                deleted.OrderId ?? Guid.Empty, unit.ProductModelId, model?.Name ?? "Eğitim kiti", model?.Sku ?? "-",
                unit.SerialNumber, unit.QrCode));
        }
        var linkedOrderIds = cohort.Students.Where(item => !item.IsDeleted && item.OrderId.HasValue)
            .Select(item => item.OrderId!.Value)
            .Distinct()
            .ToArray();
        var linkedOrder = linkedOrderIds.Length == 1
            ? await repository.GetOrderAsync(linkedOrderIds[0], cancellationToken)
            : null;
        return new PortalRentalCohortResponse(cohort.Id, cohort.CustomerId, cohort.Name, cohort.StartDate,
            cohort.EndDate, cohort.CreatedAt, linkedOrderIds.Length == 1 ? linkedOrderIds[0] : null,
            students.Count, students.Count(item => item.ProductUnitId.HasValue),
            students.OrderBy(item => item.FullName).ToArray(),
            unassigned.OrderBy(item => item.SerialNumber).ToArray(), linkedOrder?.OrderNumber, linkedOrder?.Status,
            linkedOrder is not null && IsApprovedOrderStatus(linkedOrder.Status));
    }

    private static bool IsApprovedOrderStatus(RentalOrderStatus status) =>
        status is not (RentalOrderStatus.Draft or RentalOrderStatus.PendingApproval or
            RentalOrderStatus.Rejected or RentalOrderStatus.Cancelled);

    private static ProductModel? FindModel(IReadOnlyCollection<ProductModel> models, string value)
    {
        var normalized = value.Trim();
        if (Guid.TryParse(normalized, out var productModelId))
            return models.FirstOrDefault(item => item.Id == productModelId);
        return models.FirstOrDefault(item =>
            string.Equals(item.Sku, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Name, normalized, StringComparison.CurrentCultureIgnoreCase));
    }

    private Task AddActivityAsync(Guid productUnitId, Guid? assignmentId, Guid? orderId, Guid? studentId,
        Guid actorId, string actorDisplayName, string action, string description, CancellationToken cancellationToken,
        DateTimeOffset? occurredAt = null) =>
        repository.AddProductUnitActivityAsync(ProductUnitActivity.Create(Guid.NewGuid(), productUnitId,
            assignmentId, orderId, studentId, actorId, actorDisplayName, action, description,
            occurredAt ?? TurkeyTime.Now()), cancellationToken);

    private async Task<IReadOnlyCollection<PortalFaultResponse>> MapFaultsAsync(Guid customerId,
        IReadOnlyDictionary<Guid, ProductModel> models, CancellationToken cancellationToken)
    {
        var result = new List<PortalFaultResponse>();
        foreach (var ticket in await repository.GetFaultTicketsAsync(customerId, cancellationToken))
        {
            var unit = await repository.GetProductUnitAsync(ticket.ProductUnitId, cancellationToken);
            var modelName = unit is not null && models.TryGetValue(unit.ProductModelId, out var model)
                ? model.Name : "Eğitim kiti";
            var shipments = (await repository.GetShipmentsAsync(ticket.OrderId, cancellationToken))
                .Where(item => item.FaultTicketId == ticket.Id)
                .Select(item => new PortalShipmentResponse(item.Type, item.Carrier, item.TrackingNumber, item.Status,
                    item.Events.OrderBy(evt => evt.OccurredAt).Select(evt => new PortalShipmentEventResponse(evt.Status,
                        evt.OccurredAt, evt.Location, evt.Description)).ToArray())).ToArray();
            result.Add(new PortalFaultResponse(ticket.Id, ticket.Number, ticket.ProductUnitId, modelName,
                unit?.SerialNumber ?? "-", ticket.Category, ticket.Severity, ticket.Description, ticket.Status,
                ticket.OpenedAt, ticket.History.OrderBy(item => item.OccurredAt).Select(item =>
                    new PortalFaultStatusResponse(item.Previous, item.Current, item.OccurredAt, item.Note)).ToArray(), shipments,
                ticket.ReporterName, ticket.ReporterPhone, ticket.ReporterAddress, ticket.ApprovalStatus));
        }
        return result.OrderByDescending(item => item.OpenedAt).ToArray();
    }

    private static ResolvedLocation ResolveLocation(string? district, string? city,
        double? latitude, double? longitude)
    {
        var resolvedDistrict = string.IsNullOrWhiteSpace(district) ? "Bilinmiyor" : district.Trim();
        var resolvedCity = string.IsNullOrWhiteSpace(city) ? "Bilinmiyor" : city.Trim();
        if (CoordinatesAreValid(latitude, longitude))
            return new ResolvedLocation(latitude, longitude, resolvedDistrict, resolvedCity);

        return new ResolvedLocation(null, null, resolvedDistrict, resolvedCity);
    }

    private static bool CoordinatesAreValid(double? latitude, double? longitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    private sealed record ResolvedLocation(double? Latitude, double? Longitude, string District, string City);
}









