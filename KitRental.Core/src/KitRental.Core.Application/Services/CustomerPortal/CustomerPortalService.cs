using KitRental.Core.Application.Abstractions;
using KitRental.Core.Application.Common;
using KitRental.Core.Application.Operations;
using KitRental.Core.Domain.Auditing;
using KitRental.Core.Domain.Customers;
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
    private const string CargoDropOffAddress = "Aras Kargo şubesine bırakılacak. İade kodu: 1234567890";

    public async Task<CustomerPortalDashboardResponse> GetDashboardAsync(Guid customerId,
        CancellationToken cancellationToken)
    {
        var data = await LoadPortalKitDataAsync(customerId, cancellationToken);
        var today = TurkeyTime.Today();
        var returnProcessStartedAssignmentIds = data.Returns
            .Where(item => item.Status is KitReturnStatus.Requested or KitReturnStatus.InTransit)
            .SelectMany(item => item.Items).Select(item => item.AssignmentId).ToHashSet();
        var returnedAssignmentIds = data.Returns.Where(item => item.Status == KitReturnStatus.Received)
            .SelectMany(item => item.Items).Select(item => item.AssignmentId).ToHashSet();
        var currentKits = data.Kits.Where(item =>
            item.AssignmentStatus is RentalAssignmentStatus.Reserved or RentalAssignmentStatus.Active &&
            !returnedAssignmentIds.Contains(item.AssignmentId)).ToArray();
        return new CustomerPortalDashboardResponse(data.Customer.Name, currentKits.Length,
            currentKits.Count(item => !string.IsNullOrWhiteSpace(item.AssignedStudentName)),
            currentKits.Count(item => string.IsNullOrWhiteSpace(item.AssignedStudentName)),
            data.Faults.Count(item => !IsCompletedFaultStatus(item.Status)),
            data.Faults.Count(item => IsCompletedFaultStatus(item.Status)),
            data.Kits.Count(item => item.AssignmentStatus == RentalAssignmentStatus.Active &&
                item.EndDate < today && !returnProcessStartedAssignmentIds.Contains(item.AssignmentId)),
            returnProcessStartedAssignmentIds.Count, returnedAssignmentIds.Count, data.KitLocations);
    }

    public async Task<CustomerPortalKitsResponse> GetKitsPageAsync(Guid customerId,
        CancellationToken cancellationToken)
    {
        var data = await LoadPortalKitDataAsync(customerId, cancellationToken);
        return new CustomerPortalKitsResponse(data.Customer.Name, data.Kits);
    }

    public async Task<CustomerPortalReturnsResponse> GetReturnsPageAsync(Guid customerId,
        CancellationToken cancellationToken)
    {
        var data = await LoadPortalKitDataAsync(customerId, cancellationToken);
        return new CustomerPortalReturnsResponse(data.Customer.Name, data.Kits,
            MapFaults(data.Faults, data.Models, data.Units),
            await MapReturnsAsync(customerId, data.Models, data.Units, cancellationToken, data.Returns,
                data.Customer));
    }

    public async Task<CustomerPortalFaultsResponse> GetFaultsPageAsync(Guid customerId,
        CancellationToken cancellationToken)
    {
        var customer = await GetCustomerAsync(customerId, cancellationToken);
        var tickets = await repository.GetFaultTicketsAsync(customerId, cancellationToken);
        var models = (await repository.GetProductModelsAsync(cancellationToken)).ToDictionary(item => item.Id);
        var units = await LoadProductUnitsAsync(tickets.Select(ticket => ticket.ProductUnitId), cancellationToken);
        return new CustomerPortalFaultsResponse(customer.Name, MapFaults(tickets, models, units));
    }

    public async Task<PortalFaultResponse> GetFaultAsync(Guid customerId, Guid faultId,
        CancellationToken cancellationToken)
    {
        var ticket = await repository.GetFaultTicketAsync(faultId, cancellationToken);
        if (ticket is null || ticket.CustomerId != customerId)
            throw new ResourceNotFoundException("Arıza kaydı bulunamadı.");
        var unit = await repository.GetProductUnitAsync(ticket.ProductUnitId, cancellationToken);
        var model = unit is null ? null : await repository.GetProductModelAsync(unit.ProductModelId, cancellationToken);
        return MapFault(ticket, unit, model);
    }

    public async Task<CustomerPortalRentalPeriodsResponse> GetRentalPeriodsPageAsync(Guid customerId,
        CancellationToken cancellationToken)
    {
        var customer = await GetCustomerAsync(customerId, cancellationToken);
        var models = await repository.GetProductModelsAsync(cancellationToken);
        return new CustomerPortalRentalPeriodsResponse(customer.Name, MapProductModels(customer, models),
            await MapRentalCohortsAsync(customerId, cancellationToken,
                models.ToDictionary(item => item.Id)));
    }

    public async Task<CustomerPortalRentalPeriodResponse> GetRentalPeriodPageAsync(Guid customerId, Guid periodId,
        CancellationToken cancellationToken)
    {
        var customer = await GetCustomerAsync(customerId, cancellationToken);
        var cohort = await GetOwnedCohortAsync(customerId, periodId, cancellationToken);
        var models = await repository.GetProductModelsAsync(cancellationToken);
        return new CustomerPortalRentalPeriodResponse(customer.Name, MapProductModels(customer, models),
            await MapRentalCohortAsync(cohort, cancellationToken,
                models.ToDictionary(item => item.Id)));
    }

    public async Task<CustomerPortalKitDetailResponse> GetKitDetailAsync(Guid customerId, Guid productUnitId,
        CancellationToken cancellationToken)
    {
        var customer = await GetCustomerAsync(customerId, cancellationToken);
        var unit = await repository.GetProductUnitAsync(productUnitId, cancellationToken)
            ?? throw new ResourceNotFoundException("Fiziksel kit bulunamadı.");
        var model = await repository.GetProductModelAsync(unit.ProductModelId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kit modeli bulunamadı.");
        var assignments = (await repository.GetAssignmentsForProductUnitAsync(productUnitId, cancellationToken))
            .Where(item => item.CustomerId == customerId && item.Status != RentalAssignmentStatus.Cancelled).ToArray();
        var assignmentOrders = new Dictionary<Guid, RentalOrder>();
        foreach (var assignment in assignments)
        {
            var order = await repository.FindOrderByLineIdAsync(assignment.OrderLineId, cancellationToken);
            if (order is not null && order.CustomerId == customerId && order.Type == OrderType.Rental)
                assignmentOrders[assignment.Id] = order;
        }
        var faults = (await repository.GetFaultTicketsAsync(customerId, cancellationToken))
            .Where(item => item.ProductUnitId == productUnitId).ToArray();
        var returns = await repository.GetKitReturnRequestsAsync(customerId, cancellationToken);
        var cohorts = await repository.GetRentalCohortsAsync(customerId, cancellationToken);
        var locations = await repository.GetKitLocationEventsForCustomerAsync(customerId, cancellationToken);
        var returnedIds = returns.Where(item => item.Status == KitReturnStatus.Received)
            .SelectMany(item => item.Items).Select(item => item.AssignmentId).ToHashSet();
        var deliveryIds = locations.Where(item => item.Source == KitLocationEventSource.DeliveryReceipt &&
                item.AssignmentId.HasValue).Select(item => item.AssignmentId!.Value).ToHashSet();
        var studentsByAssignment = cohorts.SelectMany(cohort => cohort.Students
                .Where(student => !student.IsDeleted && student.AssignmentId.HasValue)
                .Select(student => new PortalLinkedStudent(student.AssignmentId, student.ProductUnitId,
                    student.FullName, student.GuardianPhone, student.AddressLine, cohort.Name,
                    student.OrderId.HasValue && assignmentOrders.Values.Any(order =>
                        order.Id == student.OrderId.Value && IsApprovedOrderStatus(order.Status)))))
            .GroupBy(item => item.AssignmentId!.Value).ToDictionary(group => group.Key, group => group.First());
        var unitStudents = cohorts.SelectMany(cohort => cohort.Students
                .Where(student => !student.IsDeleted && student.ProductUnitId == productUnitId)
                .Select(student => new PortalLinkedStudent(student.AssignmentId, student.ProductUnitId,
                    student.FullName, student.GuardianPhone, student.AddressLine, cohort.Name,
                    student.OrderId.HasValue && assignmentOrders.Values.Any(order =>
                        order.Id == student.OrderId.Value && IsApprovedOrderStatus(order.Status)))))
            .ToArray();
        var kitRows = assignments.Where(item => assignmentOrders.ContainsKey(item.Id)).Select(assignment =>
        {
            var order = assignmentOrders[assignment.Id];
            var student = studentsByAssignment.TryGetValue(assignment.Id, out var assignedStudent)
                ? assignedStudent
                : unitStudents.Length == 1 ? unitStudents[0] : null;
            return new PortalKitResponse(unit.Id, assignment.Id, order.Id, order.OrderNumber, model.Name, model.Sku,
                model.ImageUrl, unit.SerialNumber, unit.QrCode, unit.Status, assignment.Status,
                order.Period!.Value.StartDate, order.Period.Value.EndDate,
                faults.Count(item => !IsCompletedFaultStatus(item.Status)), deliveryIds.Contains(assignment.Id),
                student?.FullName, student?.GuardianPhone, student?.AddressLine, student?.CohortName,
                returnedIds.Contains(assignment.Id), student?.StudentOrderLocked ?? false);
        }).ToArray();
        var kit = kitRows
            .OrderByDescending(item => item.AssignmentStatus == RentalAssignmentStatus.Active && !item.IsReturned)
            .ThenByDescending(item => item.AssignmentStatus == RentalAssignmentStatus.Active)
            .ThenByDescending(item => item.EndDate).ThenByDescending(item => item.StartDate)
            .FirstOrDefault() ?? throw new ResourceNotFoundException("Fiziksel kit bulunamadı.");
        var mappedFaults = faults.Select(item => MapFault(item, unit, model))
            .OrderByDescending(item => item.OpenedAt).ToArray();
        var relatedReturnRequests = returns.Where(item =>
            item.Items.Any(returnItem => returnItem.ProductUnitId == productUnitId)).ToArray();
        var returnUnits = await LoadProductUnitsAsync(relatedReturnRequests.SelectMany(item => item.Items)
            .Select(item => item.ProductUnitId), cancellationToken);
        var returnModels = (await repository.GetProductModelsAsync(cancellationToken))
            .ToDictionary(item => item.Id);
        var mappedReturns = (await MapReturnsAsync(customerId, returnModels, returnUnits, cancellationToken,
                relatedReturnRequests, customer))
            .OrderByDescending(item => item.CreatedAt).ToArray();
        var rentalHistory = cohorts.SelectMany(cohort => cohort.Students
                .Where(student => student.ProductUnitId == productUnitId)
                .Select(student =>
                {
                    var delivery = student.AssignmentId.HasValue
                        ? locations.Where(item => item.AssignmentId == student.AssignmentId.Value &&
                                item.Source == KitLocationEventSource.DeliveryReceipt)
                            .OrderByDescending(item => item.OccurredAt).ThenByDescending(item => item.Id).FirstOrDefault()
                        : null;
                    return new PortalKitRentalHistoryResponse(student.FullName,
                        delivery?.AddressLine ?? student.AddressLine, cohort.Name,
                        student.OrderId.HasValue
                            ? assignmentOrders.Values.FirstOrDefault(item => item.Id == student.OrderId.Value)?.OrderNumber
                            : null,
                        cohort.StartDate, cohort.EndDate, delivery?.OccurredAt);
                }))
            .OrderByDescending(item => item.DeliveredAt ?? DateTimeOffset.MinValue)
            .ThenByDescending(item => item.StartDate).ToArray();
        PortalKitLocationResponse? currentLocation = null;
        if (kit.AssignmentStatus == RentalAssignmentStatus.Active && !kit.IsReturned)
        {
            var latest = locations.Where(item => item.ProductUnitId == productUnitId)
                .OrderByDescending(item => item.OccurredAt).ThenByDescending(item => item.Id).FirstOrDefault();
            currentLocation = new PortalKitLocationResponse(unit.Id, model.Id, model.Name, model.Sku,
                unit.SerialNumber, latest?.ContactName ?? customer.Name,
                latest?.AddressLine ?? customer.Addresses.FirstOrDefault()?.Line1 ?? string.Empty,
                (int)unit.Status, latest?.Latitude, latest?.Longitude);
        }
        return new CustomerPortalKitDetailResponse(kit, currentLocation, mappedFaults, mappedReturns, rentalHistory);
    }

    public async Task<PortalFaultFormContextResponse> GetFaultFormContextAsync(Guid customerId, Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var customer = await GetCustomerAsync(customerId, cancellationToken);
        var assignment = await repository.GetRentalAssignmentAsync(assignmentId, cancellationToken);
        if (assignment is null || assignment.CustomerId != customerId ||
            assignment.Status is not (RentalAssignmentStatus.Reserved or RentalAssignmentStatus.Active))
            throw new ResourceNotFoundException("Arıza bildirilebilecek kiralama kaydı bulunamadı.");
        var returns = await repository.GetKitReturnRequestsAsync(customerId, cancellationToken);
        if (returns.Where(item => item.Status == KitReturnStatus.Received).SelectMany(item => item.Items)
            .Any(item => item.AssignmentId == assignmentId))
            throw new ResourceNotFoundException("İade edilmiş kit için arıza kaydı açılamaz.");
        var unit = await repository.GetProductUnitAsync(assignment.ProductUnitId, cancellationToken)
            ?? throw new ResourceNotFoundException("Fiziksel kit bulunamadı.");
        var model = await repository.GetProductModelAsync(unit.ProductModelId, cancellationToken);
        var student = (await repository.GetRentalCohortsAsync(customerId, cancellationToken))
            .SelectMany(item => item.Students).FirstOrDefault(item => !item.IsDeleted && item.AssignmentId == assignmentId);
        var locations = await repository.GetKitLocationEventsForCustomerAsync(customerId, cancellationToken);
        var latestLocation = locations.Where(item => item.ProductUnitId == unit.Id &&
                item.AssignmentId == assignmentId)
            .OrderByDescending(item => item.OccurredAt).ThenByDescending(item => item.Id).FirstOrDefault();
        var delivery = locations.Where(item => item.AssignmentId == assignmentId &&
                item.Source == KitLocationEventSource.DeliveryReceipt)
            .OrderByDescending(item => item.OccurredAt).ThenByDescending(item => item.Id).FirstOrDefault();
        var address = customer.Addresses.FirstOrDefault();
        return new PortalFaultFormContextResponse(assignmentId, model?.Name ?? "Eğitim kiti", unit.SerialNumber,
            FirstNotEmpty(latestLocation?.ContactName, delivery?.ContactName, student?.FullName,
                address?.ContactName) ?? string.Empty,
            FirstNotEmpty(latestLocation?.ContactPhone, delivery?.ContactPhone, student?.GuardianPhone,
                address?.Phone) ?? string.Empty,
            FirstNotEmpty(latestLocation?.AddressLine, delivery?.AddressLine, student?.AddressLine,
                address?.Line1) ?? string.Empty);
    }

    private async Task<PortalKitData> LoadPortalKitDataAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await GetCustomerAsync(customerId, cancellationToken);
        var models = (await repository.GetProductModelsAsync(cancellationToken)).ToDictionary(item => item.Id);
        var orders = await repository.GetOrdersAsync(customerId, cancellationToken);
        var rentalOrders = orders.Where(item => item.Type == OrderType.Rental).ToArray();
        var assignments = await repository.GetAssignmentsForOrdersAsync(rentalOrders.Select(item => item.Id).ToArray(),
            cancellationToken);
        var units = await LoadProductUnitsAsync(assignments.Select(item => item.ProductUnitId), cancellationToken);
        var faults = await repository.GetFaultTicketsAsync(customerId, cancellationToken);
        var returns = await repository.GetKitReturnRequestsAsync(customerId, cancellationToken);
        var cohorts = await repository.GetRentalCohortsAsync(customerId, cancellationToken);
        var locations = await repository.GetKitLocationEventsForCustomerAsync(customerId, cancellationToken);
        var ordersById = orders.ToDictionary(item => item.Id);
        var linkedStudents = cohorts.SelectMany(cohort => cohort.Students
            .Where(student => !student.IsDeleted && (student.AssignmentId.HasValue || student.ProductUnitId.HasValue))
            .Select(student => new PortalLinkedStudent(student.AssignmentId, student.ProductUnitId, student.FullName,
                student.GuardianPhone, student.AddressLine, cohort.Name,
                student.OrderId.HasValue && ordersById.TryGetValue(student.OrderId.Value, out var order) &&
                IsApprovedOrderStatus(order.Status)))).ToArray();
        var studentsByAssignment = linkedStudents.Where(item => item.AssignmentId.HasValue)
            .GroupBy(item => item.AssignmentId!.Value).ToDictionary(group => group.Key, group => group.First());
        var studentsByUnit = linkedStudents.Where(item => item.ProductUnitId.HasValue)
            .GroupBy(item => item.ProductUnitId!.Value).Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.First());
        var latestLocationsByUnit = locations.GroupBy(item => item.ProductUnitId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.OccurredAt)
                .ThenByDescending(item => item.Id).First());
        var deliveryAssignmentIds = locations.Where(item => item.Source == KitLocationEventSource.DeliveryReceipt &&
                item.AssignmentId.HasValue).Select(item => item.AssignmentId!.Value).ToHashSet();
        var returnStartedIds = returns.Where(item => item.Status is KitReturnStatus.Requested or KitReturnStatus.InTransit)
            .SelectMany(item => item.Items).Select(item => item.AssignmentId).ToHashSet();
        var returnedIds = returns.Where(item => item.Status == KitReturnStatus.Received)
            .SelectMany(item => item.Items).Select(item => item.AssignmentId).ToHashSet();
        var assignmentsByLine = assignments.GroupBy(item => item.OrderLineId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var kits = new List<PortalKitResponse>();
        var kitLocations = new List<PortalKitLocationResponse>();
        var today = TurkeyTime.Today();
        foreach (var order in rentalOrders)
        foreach (var line in order.Lines)
        {
            if (!assignmentsByLine.TryGetValue(line.Id, out var lineAssignments)) continue;
            foreach (var assignment in lineAssignments.Where(item => item.Status != RentalAssignmentStatus.Cancelled))
            {
                if (!units.TryGetValue(assignment.ProductUnitId, out var unit) ||
                    !models.TryGetValue(unit.ProductModelId, out var model)) continue;
                var openFaultCount = faults.Count(ticket => ticket.ProductUnitId == unit.Id &&
                    !IsCompletedFaultStatus(ticket.Status));
                var student = studentsByAssignment.TryGetValue(assignment.Id, out var byAssignment)
                    ? byAssignment
                    : studentsByUnit.TryGetValue(unit.Id, out var byUnit) ? byUnit : null;
                kits.Add(new PortalKitResponse(unit.Id, assignment.Id, order.Id, order.OrderNumber, model.Name,
                    model.Sku, model.ImageUrl, unit.SerialNumber, unit.QrCode, unit.Status, assignment.Status,
                    order.Period!.Value.StartDate, order.Period.Value.EndDate, openFaultCount,
                    deliveryAssignmentIds.Contains(assignment.Id), student?.FullName, student?.GuardianPhone,
                    student?.AddressLine, student?.CohortName, returnedIds.Contains(assignment.Id),
                    student?.StudentOrderLocked ?? false));
                if (assignment.Status != RentalAssignmentStatus.Active || returnedIds.Contains(assignment.Id)) continue;
                var category = GetKitLocationCategory(unit.Status, openFaultCount > 0,
                    returnStartedIds.Contains(assignment.Id), order.Period.Value.EndDate < today);
                var location = latestLocationsByUnit.GetValueOrDefault(unit.Id);
                kitLocations.Add(new PortalKitLocationResponse(unit.Id, unit.ProductModelId, model.Name, model.Sku,
                    unit.SerialNumber, location?.ContactName ?? order.DeliveryAddress.ContactName,
                    location?.AddressLine ?? order.DeliveryAddress.Line1, (int)unit.Status,
                    location?.Latitude, location?.Longitude, category));
            }
        }
        return new PortalKitData(customer, models, orders, faults, returns, cohorts, locations, units,
            kits.OrderByDescending(item => item.AssignmentStatus).ThenBy(item => item.KitName).ToArray(),
            kitLocations.OrderBy(item => item.SerialNumber).ToArray());
    }

    private async Task<Customer> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken) =>
        await repository.GetCustomerAsync(customerId, cancellationToken)
        ?? throw new ResourceNotFoundException("Müşteri hesabı bulunamadı.");

    private static IReadOnlyCollection<PortalProductModelResponse> MapProductModels(Customer customer,
        IReadOnlyCollection<ProductModel> models) => FilterAvailableProductModels(customer, models)
        .Select(item => new PortalProductModelResponse(item.Id, item.Name, item.Sku, item.Description, item.ImageUrl))
        .ToArray();

    private static bool IsCompletedFaultStatus(FaultStatus status) =>
        status is FaultStatus.Resolved or FaultStatus.RemoteResolved or FaultStatus.Rejected or FaultStatus.Closed;

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
        CancellationToken cancellationToken) => MapReturnsAsync(customerId, null, null, cancellationToken);

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
        var customer = await repository.GetCustomerAsync(command.CustomerId, cancellationToken)
            ?? throw new ResourceNotFoundException("Müşteri hesabı bulunamadı.");
        if (!customer.CanUseProductModel(command.ProductModelId))
            throw new ForbiddenException("Bu eğitim kiti müşterinin kullanımına açık değil.");
        var student = command.Id.HasValue
            ? cohort.UpdateStudent(command.Id.Value, command.FullName, command.GuardianPhone, command.AddressLine,
                command.ProductModelId)
            : cohort.AddStudent(command.FullName, command.GuardianPhone, command.AddressLine, command.ProductModelId);
        await SyncLinkedUnapprovedOrderAfterStudentChangeAsync(customer, cohort, command.ActorId, cancellationToken);
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
        var customer = await repository.GetCustomerAsync(customerId, cancellationToken)
            ?? throw new ResourceNotFoundException("Müşteri hesabı bulunamadı.");
        var models = FilterAvailableProductModels(customer, await repository.GetProductModelsAsync(cancellationToken));
        foreach (var row in rows)
        {
            var model = FindModel(models, row.ProductModel)
                ?? throw new ResourceNotFoundException($"{row.ProductModel} eğitim kiti bulunamadı.");
            cohort.AddStudent(row.FullName, row.GuardianPhone, row.AddressLine, model.Id);
        }
        await SyncLinkedUnapprovedOrderAfterStudentChangeAsync(customer, cohort, actorId, cancellationToken);
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), actorId, nameof(RentalCohort),
            cohort.Id, "StudentsImported", null, $"{rows.Count} öğrenci", TurkeyTime.Now()), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await MapRentalCohortAsync(cohort, cancellationToken);
    }

    public async Task RemoveRentalCohortStudentAsync(Guid customerId, Guid cohortId, Guid studentId,
        Guid actorId, string actorDisplayName, CancellationToken cancellationToken)
    {
        var cohort = await GetOwnedCohortAsync(customerId, cohortId, cancellationToken);
        var student = cohort.Students.SingleOrDefault(item => item.Id == studentId && !item.IsDeleted)
            ?? throw new ResourceNotFoundException("Öğrenci bulunamadı.");
        if (student.ProductUnitId.HasValue || student.AssignmentId.HasValue)
            throw new ConflictException("rental_cohort.student_kit_assigned",
                "Fiziksel kit atanmış öğrenci silinemez.");
        if (student.OrderId.HasValue &&
            await repository.GetKargonomiShipmentAsync(student.OrderId.Value, student.Id, cancellationToken) is not null)
            throw new ConflictException("rental_cohort.shipment_started",
                "Kargo süreci başlatılan öğrenci silinemez.");
        var studentName = student.FullName;
        var linkedOrder = await GetSingleLinkedOrderAsync(cohort, cancellationToken);
        cohort.RemoveStudent(studentId);
        if (linkedOrder is not null && IsApprovedOrderStatus(linkedOrder.Status))
        {
            linkedOrder.RemoveOneKitRequirement(student.ProductModelId);
        }
        else
        {
            var customer = await repository.GetCustomerAsync(customerId, cancellationToken)
                ?? throw new ResourceNotFoundException("Müşteri hesabı bulunamadı.");
            await SyncLinkedUnapprovedOrderAfterStudentChangeAsync(customer, cohort, actorId, cancellationToken,
                linkedOrder);
        }
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), actorId, nameof(RentalCohort),
            cohort.Id, "StudentRemoved", studentName, null, TurkeyTime.Now()), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<KitReturnRequest> CreatePortalStudentReturnAsync(CreatePortalStudentReturnCommand command,
        CancellationToken cancellationToken)
    {
        if (new[] { command.RequesterName, command.RequesterPhone, command.ReturnAddress }
            .Any(string.IsNullOrWhiteSpace))
            throw new DomainException("kit_return.required_fields",
                "İade için ad soyad, telefon ve adres zorunludur.");
        if (!command.ReturnReason.HasValue)
            throw new DomainException("kit_return.reason_required", "İade nedeni seçilmelidir.");
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
            command.RequesterName, command.RequesterPhone, command.ReturnAddress, null, null, command.ReturnReason);
        await repository.AddKitReturnRequestAsync(request, cancellationToken);
        await repository.AddKitLocationEventAsync(KitLocationEvent.Create(Guid.NewGuid(), student.ProductUnitId.Value,
            student.AssignmentId.Value, student.OrderId.Value, command.CustomerId, KitLocationEventSource.ReturnRequest,
            request.Id, command.RequesterName, command.RequesterPhone, command.ReturnAddress,
            null, null, now, command.ActorId), cancellationToken);
        await AddActivityAsync(student.ProductUnitId.Value, student.AssignmentId, student.OrderId, student.Id,
            command.ActorId, command.ActorDisplayName, "İade talebi oluşturuldu",
            $"{command.RequesterName.Trim()} iade talebi oluşturdu.", cancellationToken, now);
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), command.ActorId,
            nameof(KitReturnRequest), request.Id, "StudentReturnRequested", null,
            command.RequesterName.Trim(), now), cancellationToken);
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
        if (!Enum.IsDefined(command.DeliveryMethod))
            throw new DomainException("kit_return.delivery_method_required", "Teslimat şekli seçilmelidir.");
        var unit = await repository.GetProductUnitByQrCodeAsync(command.QrCode, cancellationToken)
            ?? throw new ResourceNotFoundException("Bu QR kodla eşleşen fiziksel kit bulunamadı.");
        if (unit.Status is not (ProductUnitStatus.WithCustomer or ProductUnitStatus.ReturnInTransit))
            throw new ConflictException("kit_return.unit_not_with_customer", "Bu kit şu anda müşteride görünmüyor.");
        var assignment = (await repository.GetAssignmentsForProductUnitAsync(unit.Id, cancellationToken))
            .Where(item => item.Status == RentalAssignmentStatus.Active)
            .FirstOrDefault()
            ?? throw new ConflictException("kit_return.no_active_rental", "Bu kit için aktif bir kiralama bulunmuyor.");
        var activeReturns = await repository.GetKitReturnRequestsAsync(assignment.CustomerId, cancellationToken);
        var existingRequest = activeReturns.Where(item => item.Status != KitReturnStatus.Received)
            .FirstOrDefault(item => item.Items.Any(returnItem => returnItem.AssignmentId == assignment.Id));
        var order = await repository.FindOrderByLineIdAsync(assignment.OrderLineId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kiralama siparişi bulunamadı.");
        var now = TurkeyTime.Now();
        var returnAddress = command.DeliveryMethod == KitReturnDeliveryMethod.DropOffToCargo
            ? CargoDropOffAddress
            : command.ReturnAddress;
        var latitude = command.DeliveryMethod != KitReturnDeliveryMethod.DropOffToCargo &&
            CoordinatesAreValid(command.Latitude, command.Longitude)
                ? command.Latitude
                : null;
        var longitude = command.DeliveryMethod != KitReturnDeliveryMethod.DropOffToCargo &&
            CoordinatesAreValid(command.Latitude, command.Longitude)
                ? command.Longitude
                : null;
        var request = existingRequest;
        var isUpdate = request is not null;
        if (request is null)
        {
            request = KitReturnRequest.CreatePublic(Guid.NewGuid(), assignment.CustomerId, now, PublicActorId,
                [new KitReturnItem(Guid.NewGuid(), assignment.Id, assignment.ProductUnitId, order.Id)],
                command.RequesterName, command.RequesterPhone,
                returnAddress, latitude, longitude, command.ReturnReason,
                command.DeliveryMethod);
            await repository.AddKitReturnRequestAsync(request, cancellationToken);
        }
        else
        {
            request.UpdatePublicDetails(command.RequesterName, command.RequesterPhone, returnAddress,
                latitude, longitude, command.ReturnReason, command.DeliveryMethod);
        }
        await repository.AddKitLocationEventAsync(KitLocationEvent.Create(Guid.NewGuid(), unit.Id, assignment.Id,
            order.Id, assignment.CustomerId, KitLocationEventSource.ReturnRequest, request.Id,
            command.RequesterName, command.RequesterPhone, returnAddress, latitude, longitude, now, PublicActorId),
            cancellationToken);
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), PublicActorId,
            nameof(KitReturnRequest), request.Id, isUpdate ? "PublicReturnUpdated" : "PublicReturnRequested", null,
            command.RequesterName.Trim(), now), cancellationToken);
        await AddActivityAsync(unit.Id, assignment.Id, order.Id, null, PublicActorId, command.RequesterName,
            isUpdate ? "İade talebi güncellendi" : "İade talebi oluşturuldu",
            isUpdate
                ? $"{command.RequesterName.Trim()} iade talebini güncelledi."
                : $"{command.RequesterName.Trim()} iade talebi oluşturdu.",
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
            await repository.AddKitLocationEventAsync(KitLocationEvent.Create(Guid.NewGuid(), unit.Id,
                item.AssignmentId, item.OrderId, request.CustomerId, KitLocationEventSource.ReturnRequest,
                request.Id, "Robotik Bilim Atölye", string.Empty, "Robotik Bilim Atölye", null, null, now, actorId),
                cancellationToken);
            var assignment = await repository.GetRentalAssignmentAsync(item.AssignmentId, cancellationToken);
            if (assignment?.Status == RentalAssignmentStatus.Active) assignment.Complete();
            var cohort = customerCohorts.FirstOrDefault(candidate =>
                candidate.Students.Any(student => student.AssignmentId == item.AssignmentId && !student.IsDeleted));
            var student = cohort?.Students.SingleOrDefault(candidate =>
                candidate.AssignmentId == item.AssignmentId && !candidate.IsDeleted);
            if (student is not null && cohort is not null)
            {
                await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), actorId,
                    nameof(RentalCohort), cohort.Id, "StudentKitReturnCompleted", student.FullName,
                    unit.SerialNumber, now), cancellationToken);
            }
            var studentDescription = student is null
                ? "İade teslim alındı; kit yeniden kullanılabilir stoka alındı."
                : $"İade teslim alındı; {student.FullName} öğrencisinin kit ilişkisi geçmiş kayıt olarak korundu.";
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
        IReadOnlyDictionary<Guid, ProductModel>? models,
        IReadOnlyDictionary<Guid, ProductUnit>? units,
        CancellationToken cancellationToken,
        IReadOnlyCollection<KitReturnRequest>? requests = null,
        Customer? customer = null)
    {
        var customers = customer is null
            ? (await repository.GetCustomersAsync(cancellationToken)).ToDictionary(item => item.Id)
            : new Dictionary<Guid, Customer> { [customer.Id] = customer };
        models ??= (await repository.GetProductModelsAsync(cancellationToken)).ToDictionary(x => x.Id);
        requests ??= await repository.GetKitReturnRequestsAsync(customerId, cancellationToken);
        units ??= await LoadProductUnitsAsync(requests.SelectMany(item => item.Items)
            .Select(item => item.ProductUnitId), cancellationToken);
        var result = new List<PortalKitReturnResponse>();
        foreach (var request in requests)
        {
            var items = new List<PortalKitReturnItemResponse>();
            foreach (var item in request.Items)
            {
                units.TryGetValue(item.ProductUnitId, out var unit);
                items.Add(new PortalKitReturnItemResponse(item.AssignmentId, item.ProductUnitId, item.OrderId,
                    unit is not null && models.TryGetValue(unit.ProductModelId, out var model) ? model.Name : "Eğitim kiti",
                    unit?.SerialNumber ?? "-"));
            }
            result.Add(new PortalKitReturnResponse(request.Id, request.CustomerId,
                customers.TryGetValue(request.CustomerId, out var requestCustomer) ? requestCustomer.Name : "Müşteri",
                request.Status, request.Carrier, request.TrackingNumber, request.CreatedAt, request.ShippedAt,
                request.RequesterName, request.RequesterPhone,
                request.ReturnAddress, request.Latitude, request.Longitude, request.DeliveryMethod, items));
        }
        return result;
    }

    public async Task<PublicKitReturnContextResponse?> GetPublicKitReturnContextAsync(string qrCode,
        CancellationToken cancellationToken)
    {
        var unit = await repository.GetProductUnitByQrCodeAsync(qrCode, cancellationToken);
        if (unit is null) return null;
        var assignment = (await repository.GetAssignmentsForProductUnitAsync(unit.Id, cancellationToken))
            .Where(item => item.Status == RentalAssignmentStatus.Active)
            .FirstOrDefault();
        if (assignment is null) return null;
        var request = (await repository.GetKitReturnRequestsAsync(assignment.CustomerId, cancellationToken))
            .Where(item => item.Status != KitReturnStatus.Received)
            .FirstOrDefault(item => item.Items.Any(returnItem => returnItem.AssignmentId == assignment.Id));
        if (request is null) return null;
        var isDropOff = request.DeliveryMethod == KitReturnDeliveryMethod.DropOffToCargo;
        return new PublicKitReturnContextResponse(request.Id, request.RequesterName, request.RequesterPhone,
            isDropOff ? null : request.ReturnAddress,
            request.Latitude, request.Longitude,
            request.ReturnReason, request.DeliveryMethod);
    }

    public async Task<IReadOnlyCollection<PortalOrderResponse>> GetOrderSummariesAsync(Guid? customerId,
        CancellationToken cancellationToken)
    {
        var customers = (await repository.GetCustomersAsync(cancellationToken)).ToDictionary(item => item.Id);
        var models = (await repository.GetProductModelsAsync(cancellationToken)).ToDictionary(item => item.Id);
        var cohortsByOrderId = (await repository.GetRentalCohortsAsync(customerId, cancellationToken))
            .SelectMany(cohort => cohort.Students
                .Where(student => student.OrderId.HasValue)
                .Select(student => new { OrderId = student.OrderId!.Value, Cohort = cohort }))
            .GroupBy(item => item.OrderId)
            .ToDictionary(group => group.Key, group => group.First().Cohort);
        var result = new List<PortalOrderResponse>();
        foreach (var order in await repository.GetOrdersAsync(customerId, cancellationToken))
        {
            var assignedKitCount = order.Type == OrderType.Rental
                ? (await repository.GetAssignmentsForOrderAsync(order.Id, cancellationToken))
                    .Count(assignment => assignment.Status != RentalAssignmentStatus.Cancelled)
                : order.ProductUnits.Count;
            cohortsByOrderId.TryGetValue(order.Id, out var cohort);
            result.Add(new PortalOrderResponse(order.Id, order.OrderNumber, order.CustomerId,
                customers.TryGetValue(order.CustomerId, out var customer) ? customer.Name : "Müşteri",
                order.Type, order.Status, order.Period?.StartDate, order.Period?.EndDate, order.CreatedAt,
                order.Lines.Select(line => new PortalOrderLineResponse(line.ProductModelId,
                    models.TryGetValue(line.ProductModelId, out var model) ? model.Name : "Eğitim kiti",
                    models.TryGetValue(line.ProductModelId, out model) ? model.Sku : "-", line.Quantity)).ToArray(),
                assignedKitCount, cohort?.Name));
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
        if (new[] { command.ReporterName, command.ReporterPhone, command.ReporterAddress, command.Description }
            .Any(string.IsNullOrWhiteSpace))
            throw new DomainException("fault.required_fields",
                "Arıza için bildiren kişi, telefon, adres ve arıza nedeni zorunludur.");
        var assignment = await repository.GetRentalAssignmentAsync(command.AssignmentId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kiralama kaydı bulunamadı.");
        if (assignment.CustomerId != command.CustomerId ||
            assignment.Status is not (RentalAssignmentStatus.Reserved or RentalAssignmentStatus.Active))
            throw new ForbiddenException("Yalnızca hesabınıza ait atanmış ve iade edilmemiş kitler için arıza kaydı açabilirsiniz.");
        var customerReturns = await repository.GetKitReturnRequestsAsync(command.CustomerId, cancellationToken);
        if (customerReturns.Where(item => item.Status == KitReturnStatus.Received)
            .SelectMany(item => item.Items)
            .Any(item => item.AssignmentId == assignment.Id))
            throw new ConflictException("fault.returned_kit_readonly", "İade edilmiş kit üzerinde arıza kaydı açılamaz.");
        var order = await repository.FindOrderByLineIdAsync(assignment.OrderLineId, cancellationToken)
            ?? throw new ResourceNotFoundException("Kiralama siparişi bulunamadı.");
        var ticket = await operationsService.OpenFaultAsync(new OpenFaultCommand(command.CustomerId, order.Id, assignment.Id,
            assignment.ProductUnitId, "Müşteri paneli bildirimi", FaultSeverity.Medium, command.Description, command.ActorId,
            command.ReporterName, command.ReporterPhone, command.ReporterAddress, null,
            null, FaultOrigin.CustomerPortal),
            cancellationToken);
        await repository.AddKitLocationEventAsync(KitLocationEvent.Create(Guid.NewGuid(), assignment.ProductUnitId,
            assignment.Id, order.Id, command.CustomerId, KitLocationEventSource.FaultReport, ticket.Id,
            command.ReporterName, command.ReporterPhone, command.ReporterAddress, null, null, TurkeyTime.Now(),
            command.ActorId), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
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
        var linkedOrder = await GetSingleLinkedOrderAsync(cohort, cancellationToken);
        if (linkedOrder is not null && IsApprovedOrderStatus(linkedOrder.Status))
            throw new ConflictException("rental_cohort.students_locked",
                "Onaylanmış kiralama dönemlerinde öğrenci ekleme veya güncelleme yapılamaz.");
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
            .Select(group => (ProductModelId: group.Key, Quantity: group.Count()))
            .ToArray();
        linkedOrder.UpdateUnapprovedRentalPlan(cohort.StartDate, cohort.EndDate, lines);
    }

    private async Task SyncLinkedUnapprovedOrderAfterStudentChangeAsync(Customer customer, RentalCohort cohort,
        Guid actorId, CancellationToken cancellationToken, RentalOrder? linkedOrder = null)
    {
        var activeStudents = cohort.Students.Where(item => !item.IsDeleted).ToArray();
        linkedOrder ??= await GetSingleLinkedOrderAsync(cohort, cancellationToken);
        if (activeStudents.Length == 0)
        {
            if (linkedOrder is not null && !IsApprovedOrderStatus(linkedOrder.Status))
            {
                await repository.RemoveOrderAsync(linkedOrder, cancellationToken);
                await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), actorId, nameof(RentalOrder),
                    linkedOrder.Id, "RemovedAfterStudentListEmptied", linkedOrder.OrderNumber, null,
                    TurkeyTime.Now()), cancellationToken);
            }
            return;
        }

        var lines = activeStudents
            .GroupBy(item => item.ProductModelId)
            .Select(group => (ProductModelId: group.Key, Quantity: group.Count()))
            .ToArray();
        if (linkedOrder is not null)
        {
            linkedOrder.UpdateUnapprovedRentalPlan(cohort.StartDate, cohort.EndDate, lines);
            cohort.LinkActiveStudentsToOrder(linkedOrder.Id);
            return;
        }

        var address = customer.Addresses.FirstOrDefault()
            ?? throw new ConflictException("customer.address_required",
                "Sipariş oluşturmak için müşteri teslimat adresi bulunmalıdır.");
        var order = await operationsService.CreateOrderAsync(new CreateOrderCommand(customer.Id, address.Id,
            cohort.StartDate, cohort.EndDate,
            lines.Select(line => new OrderLineCommand(line.ProductModelId, line.Quantity)).ToArray(), actorId),
            cancellationToken);
        cohort.LinkActiveStudentsToOrder(order.Id);
        await repository.AddAuditEntryAsync(new AuditEntry(Guid.NewGuid(), actorId, nameof(RentalCohort),
            cohort.Id, "OrderAutoCreated", null, order.OrderNumber, TurkeyTime.Now()), cancellationToken);
    }

    private async Task<RentalOrder?> GetSingleLinkedOrderAsync(RentalCohort cohort,
        CancellationToken cancellationToken)
    {
        var activeOrderIds = cohort.Students
            .Where(item => !item.IsDeleted && item.OrderId.HasValue)
            .Select(item => item.OrderId!.Value)
            .Distinct()
            .ToArray();
        if (activeOrderIds.Length > 1)
            throw new ConflictException("rental_cohort.multiple_orders",
                "Bu sipariş dönemi birden fazla siparişe bağlı olduğu için düzenlenemez.");
        if (activeOrderIds.Length == 1)
            return await repository.GetOrderAsync(activeOrderIds[0], cancellationToken)
                ?? throw new ResourceNotFoundException("Kiralama siparişi bulunamadı.");

        RentalOrder? latestExistingOrder = null;
        foreach (var historicalOrderId in cohort.Students
                     .Where(item => item.OrderId.HasValue)
                     .Select(item => item.OrderId!.Value)
                     .Distinct())
        {
            var historicalOrder = await repository.GetOrderAsync(historicalOrderId, cancellationToken);
            if (historicalOrder is not null &&
                (latestExistingOrder is null || historicalOrder.CreatedAt > latestExistingOrder.CreatedAt))
                latestExistingOrder = historicalOrder;
        }
        return latestExistingOrder;
    }

    private async Task<IReadOnlyCollection<PortalRentalCohortResponse>> MapRentalCohortsAsync(Guid customerId,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<Guid, ProductModel>? models = null,
        IReadOnlyCollection<KitReturnRequest>? returns = null,
        IReadOnlyCollection<KitLocationEvent>? locations = null,
        IReadOnlyDictionary<Guid, ProductUnit>? units = null,
        IReadOnlyCollection<RentalCohort>? cohorts = null)
    {
        models ??= (await repository.GetProductModelsAsync(cancellationToken)).ToDictionary(item => item.Id);
        returns ??= await repository.GetKitReturnRequestsAsync(customerId, cancellationToken);
        locations ??= await repository.GetKitLocationEventsForCustomerAsync(customerId, cancellationToken);
        cohorts ??= await repository.GetRentalCohortsAsync(customerId, cancellationToken);
        units ??= await LoadProductUnitsAsync(cohorts.SelectMany(cohort => cohort.Students)
            .Where(student => student.ProductUnitId.HasValue)
            .Select(student => student.ProductUnitId!.Value), cancellationToken);
        var result = new List<PortalRentalCohortResponse>();
        foreach (var cohort in cohorts)
            result.Add(await MapRentalCohortAsync(cohort, cancellationToken, models, returns, locations, units));
        return result;
    }

    private async Task<PortalRentalCohortResponse> MapRentalCohortAsync(RentalCohort cohort,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<Guid, ProductModel>? models = null,
        IReadOnlyCollection<KitReturnRequest>? returns = null,
        IReadOnlyCollection<KitLocationEvent>? locations = null,
        IReadOnlyDictionary<Guid, ProductUnit>? units = null)
    {
        models ??= (await repository.GetProductModelsAsync(cancellationToken)).ToDictionary(item => item.Id);
        returns ??= await repository.GetKitReturnRequestsAsync(cohort.CustomerId, cancellationToken);
        locations ??= await repository.GetKitLocationEventsForCustomerAsync(cohort.CustomerId, cancellationToken);
        units ??= await LoadProductUnitsAsync(cohort.Students.Where(student => student.ProductUnitId.HasValue)
            .Select(student => student.ProductUnitId!.Value), cancellationToken);
        var activeReturnAssignmentIds = returns.Where(item => item.Status != KitReturnStatus.Received)
            .SelectMany(item => item.Items)
            .Select(item => item.AssignmentId)
            .ToHashSet();
        var completedReturnAssignmentIds = returns.Where(item => item.Status == KitReturnStatus.Received)
            .SelectMany(item => item.Items)
            .Select(item => item.AssignmentId)
            .ToHashSet();
        var deliveryEventsByAssignment = locations
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
            ProductUnit? unit = student.ProductUnitId.HasValue &&
                units.TryGetValue(student.ProductUnitId.Value, out var foundUnit)
                ? foundUnit
                : null;
            var delivery = student.AssignmentId.HasValue &&
                deliveryEventsByAssignment.TryGetValue(student.AssignmentId.Value, out var foundDelivery)
                    ? foundDelivery
                    : null;
            students.Add(new PortalRentalCohortStudentResponse(student.Id, student.FullName, student.GuardianPhone,
                student.AddressLine, student.ProductModelId, model?.Name ?? "Eğitim kiti", model?.Sku ?? "-",
                student.OrderId, student.AssignmentId, student.ProductUnitId, unit?.SerialNumber, unit?.QrCode,
                student.IsDeleted, student.AssignmentId.HasValue &&
                    activeReturnAssignmentIds.Contains(student.AssignmentId.Value),
                student.AssignmentId.HasValue && completedReturnAssignmentIds.Contains(student.AssignmentId.Value),
                delivery is not null, delivery?.ContactName, delivery?.ContactPhone, delivery?.AddressLine,
                delivery?.OccurredAt, student.PublicAddressToken, student.AddressSubmittedAt));
        }
        var assignedStudentUnitIds = cohort.Students.Where(item => !item.IsDeleted && item.ProductUnitId.HasValue)
            .Select(item => item.ProductUnitId!.Value)
            .ToHashSet();
        var unassigned = new List<PortalUnassignedCohortKitResponse>();
        foreach (var deleted in cohort.Students.Where(item => item.IsDeleted && item.ProductUnitId.HasValue))
        {
            if (!deleted.ProductUnitId.HasValue) continue;
            if (!units.TryGetValue(deleted.ProductUnitId.Value, out var unit) ||
                assignedStudentUnitIds.Contains(unit.Id)) continue;
            models.TryGetValue(unit.ProductModelId, out var model);
            unassigned.Add(new PortalUnassignedCohortKitResponse(unit.Id, deleted.AssignmentId ?? Guid.Empty,
                deleted.OrderId ?? Guid.Empty, unit.ProductModelId, model?.Name ?? "Eğitim kiti", model?.Sku ?? "-",
                unit.SerialNumber, unit.QrCode));
        }
        var linkedOrder = await GetSingleLinkedOrderAsync(cohort, cancellationToken);
        IReadOnlyCollection<PortalKargonomiShipmentResponse> shipments = [];
        if (linkedOrder is not null)
        {
            shipments = (await repository.GetKargonomiShipmentsAsync(linkedOrder.Id, cancellationToken))
                .Select(item => new PortalKargonomiShipmentResponse(item.Id, item.OrderId, item.StudentId,
                    item.Carrier, item.TrackingNumber, item.StatusLabel, item.State, item.LastError, item.UpdatedAt))
                .OrderByDescending(item => item.UpdatedAt)
                .ToArray();
        }
        return new PortalRentalCohortResponse(cohort.Id, cohort.CustomerId, cohort.Name, cohort.StartDate,
            cohort.EndDate, cohort.CreatedAt, linkedOrder?.Id,
            students.Count, students.Count(item => item.ProductUnitId.HasValue),
            students.OrderBy(item => item.FullName).ToArray(),
            unassigned.OrderBy(item => item.SerialNumber).ToArray(), linkedOrder?.OrderNumber, linkedOrder?.Status,
            linkedOrder is not null && IsApprovedOrderStatus(linkedOrder.Status), shipments);
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

    private static IReadOnlyCollection<ProductModel> FilterAvailableProductModels(
        Customer customer, IReadOnlyCollection<ProductModel> models)
    {
        var allowedIds = customer.AllowedProductModelIds.ToHashSet();
        return allowedIds.Count == 0
            ? models
            : models.Where(model => allowedIds.Contains(model.Id)).ToArray();
    }

    private async Task<IReadOnlyDictionary<Guid, ProductUnit>> LoadProductUnitsAsync(
        IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var distinctIds = ids.Where(id => id != Guid.Empty).Distinct().ToArray();
        return (await repository.GetProductUnitsByIdsAsync(distinctIds, cancellationToken))
            .ToDictionary(item => item.Id);
    }

    private Task AddActivityAsync(Guid productUnitId, Guid? assignmentId, Guid? orderId, Guid? studentId,
        Guid actorId, string actorDisplayName, string action, string description, CancellationToken cancellationToken,
        DateTimeOffset? occurredAt = null) =>
        repository.AddProductUnitActivityAsync(ProductUnitActivity.Create(Guid.NewGuid(), productUnitId,
            assignmentId, orderId, studentId, actorId, actorDisplayName, action, description,
            occurredAt ?? TurkeyTime.Now()), cancellationToken);

    private static IReadOnlyCollection<PortalFaultResponse> MapFaults(IReadOnlyCollection<FaultTicket> tickets,
        IReadOnlyDictionary<Guid, ProductModel> models, IReadOnlyDictionary<Guid, ProductUnit> units) =>
        tickets.Select(ticket =>
        {
            units.TryGetValue(ticket.ProductUnitId, out var unit);
            models.TryGetValue(unit?.ProductModelId ?? Guid.Empty, out var model);
            return MapFault(ticket, unit, model);
        }).OrderByDescending(item => item.OpenedAt).ToArray();

    private static PortalFaultResponse MapFault(FaultTicket ticket, ProductUnit? unit, ProductModel? model) =>
        new(ticket.Id, ticket.Number, ticket.ProductUnitId, model?.Name ?? "Eğitim kiti",
            unit?.SerialNumber ?? "-", ticket.Category, ticket.Severity, ticket.Description, ticket.Status,
            ticket.OpenedAt, ticket.History.OrderBy(item => item.OccurredAt).Select(item =>
                new PortalFaultStatusResponse(item.Previous, item.Current, item.OccurredAt, item.Note)).ToArray(),
            ticket.ReporterName, ticket.ReporterPhone, ticket.ReporterAddress, ticket.ApprovalStatus, ticket.Origin);

    private sealed record PortalLinkedStudent(Guid? AssignmentId, Guid? ProductUnitId, string FullName,
        string GuardianPhone, string AddressLine, string CohortName, bool StudentOrderLocked);

    private sealed record PortalKitData(Customer Customer, IReadOnlyDictionary<Guid, ProductModel> Models,
        IReadOnlyCollection<RentalOrder> Orders, IReadOnlyCollection<FaultTicket> Faults,
        IReadOnlyCollection<KitReturnRequest> Returns, IReadOnlyCollection<RentalCohort> Cohorts,
        IReadOnlyCollection<KitLocationEvent> Locations, IReadOnlyDictionary<Guid, ProductUnit> Units,
        IReadOnlyCollection<PortalKitResponse> Kits, IReadOnlyCollection<PortalKitLocationResponse> KitLocations);

    private static bool CoordinatesAreValid(double? latitude, double? longitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    private static string? FirstNotEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}









