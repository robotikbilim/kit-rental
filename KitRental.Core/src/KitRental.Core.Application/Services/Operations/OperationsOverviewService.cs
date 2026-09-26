using KitRental.Core.Application.Abstractions;
using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Rentals;
using KitRental.Core.Domain.Support;
using KitRental.SharedKernel;

namespace KitRental.Core.Application.Operations;

/// <summary>Read-only operational projections. Commands remain in OperationsService.</summary>
public sealed class OperationsOverviewService(ICoreRepository repository, TimeProvider timeProvider)
{
    public async Task<IReadOnlyCollection<OperationsOrderSummary>> GetCustomerOrderSummariesAsync(Guid customerId,
        CancellationToken cancellationToken) => (await LoadAsync(customerId, cancellationToken)).Orders;

    public async Task<OperationsDashboardResponse> GetDashboardAsync(Guid? customerId,
        CancellationToken cancellationToken)
    {
        var snapshot = await LoadAsync(customerId, cancellationToken);
        var rows = snapshot.Orders;
        var faults = snapshot.Faults;
        var priorities = rows.Where(item => item.IsOverdue || item.ShipmentFailedCount > 0 ||
                item.Status == 2 || item.IsEndingSoon || item.OpenFaultCount > 0 ||
                item.PendingReturnCount > 0 || item.MissingAddressCount > 0)
            .OrderByDescending(item => item.IsOverdue)
            .ThenByDescending(item => item.ShipmentFailedCount > 0)
            .ThenByDescending(item => item.Status == 2)
            .ThenBy(item => item.EndDate ?? DateOnly.MaxValue)
            .ThenBy(item => item.Id).Take(8).ToArray();
        return new OperationsDashboardResponse(rows.Count, rows.Sum(x => x.StudentCount),
            rows.Sum(x => x.MissingAddressCount), rows.Sum(x => x.AwaitingShipmentCount),
            rows.Sum(x => x.ShipmentInTransitCount), rows.Sum(x => x.ShipmentDeliveredCount),
            rows.Sum(x => x.PendingReturnCount), rows.Sum(x => x.InTransitReturnCount),
            rows.Sum(x => x.CompletedReturnCount),
            faults.Count(x => OperationsWorkload.MatchesFaultStage(x.Status, "review")),
            faults.Count(x => OperationsWorkload.MatchesFaultStage(x.Status, "repair")),
            faults.Count(x => OperationsWorkload.MatchesFaultStage(x.Status, "shipment")),
            faults.Count(x => OperationsWorkload.MatchesFaultStage(x.Status, "completed")),
            rows.Count(x => x.Status == 2), rows.Sum(x => x.ActiveKitCount),
            rows.Sum(x => x.ShipmentReadyCount), rows.Sum(x => x.ShipmentFailedCount),
            faults.Count(x => OperationsWorkload.IsOpenFault(x.Status)),
            rows.Count(x => x.IsOverdue), rows.Count(x => x.IsEndingSoon),
            rows.Sum(x => x.MissingReturnFormCount), timeProvider.GetUtcNow(), customerId,
            snapshot.Customers, priorities);
    }

    public async Task<OperationsOrderPage> GetOrdersAsync(OperationsOrderQuery query,
        CancellationToken cancellationToken)
    {
        if (query.EndsFrom > query.EndsTo)
            throw new DomainException("orders.invalid_date_range", "Bitiş tarihi aralığı geçersiz.");
        var snapshot = await LoadAsync(query.CustomerId, cancellationToken);
        IEnumerable<OperationsOrderSummary> rows = snapshot.Orders;
        if (query.Type.HasValue) rows = rows.Where(x => x.Type == query.Type);
        if (query.Status.HasValue) rows = rows.Where(x => x.Status == query.Status);
        if (query.EndsFrom.HasValue) rows = rows.Where(x => x.EndDate >= query.EndsFrom);
        if (query.EndsTo.HasValue) rows = rows.Where(x => x.EndDate <= query.EndsTo);
        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            var term = query.Query.Trim();
            rows = rows.Where(x => x.OrderNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (x.RentalPeriodName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                x.KitDescription.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        rows = rows.Where(x => OperationsWorkload.MatchesOrderFocus(x, query.Focus));
        rows = query.Sort switch
        {
            "end-date" => rows.OrderBy(x => x.EndDate ?? DateOnly.MaxValue).ThenBy(x => x.Id),
            "oldest" => rows.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            _ => rows.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
        };
        var filtered = rows.ToArray();
        var pageSize = Math.Clamp(query.PageSize, 10, 100);
        var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Length / (double)pageSize));
        var page = Math.Clamp(query.Page, 1, totalPages);
        return new OperationsOrderPage(page, pageSize, filtered.Length, totalPages,
            filtered.Skip((page - 1) * pageSize).Take(pageSize).ToArray(), snapshot.Customers);
    }

    private async Task<Snapshot> LoadAsync(Guid? customerId, CancellationToken cancellationToken)
    {
        // Calls are sequential because the repository shares one EF DbContext.
        var customers = (await repository.GetCustomersAsync(cancellationToken)).ToDictionary(x => x.Id);
        var orders = await repository.GetOrdersAsync(customerId, cancellationToken);
        var orderIds = orders.Select(x => x.Id).ToHashSet();
        var models = (await repository.GetProductModelsAsync(cancellationToken)).ToDictionary(x => x.Id);
        var cohorts = await repository.GetRentalCohortsAsync(customerId, cancellationToken);
        var students = cohorts.SelectMany(cohort => cohort.Students
                .Where(x => !x.IsDeleted && x.OrderId.HasValue && orderIds.Contains(x.OrderId.Value))
                .Select(student => new { Student = student, cohort.Name }))
            .ToLookup(x => x.Student.OrderId!.Value);
        var assignments = (await repository.GetAssignmentsForOrdersAsync(orderIds, cancellationToken))
            .Where(x => x.Status != RentalAssignmentStatus.Cancelled).ToLookup(x => x.OrderLineId);
        var shipments = (await repository.GetKargonomiShipmentsForOrdersAsync(orderIds, cancellationToken))
            .GroupBy(x => (x.OrderId, x.StudentId))
            .ToDictionary(x => x.Key, x => x.OrderByDescending(s => s.UpdatedAt).ThenByDescending(s => s.Id).First());
        var returns = (await repository.GetKitReturnRequestsAsync(customerId, cancellationToken))
            .SelectMany(request => request.Items.Select(item => new { Request = request, Item = item }))
            .Where(x => orderIds.Contains(x.Item.OrderId))
            .GroupBy(x => x.Item.AssignmentId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(r => r.Request.CreatedAt)
                .ThenByDescending(r => r.Request.Id).First().Request);
        var faults = (await repository.GetFaultTicketsAsync(customerId, cancellationToken)).ToArray();
        var faultsByOrder = faults.ToLookup(x => x.OrderId);
        var today = timeProvider.GetTurkeyToday();
        var rows = new List<OperationsOrderSummary>();
        foreach (var order in orders)
        {
            var operational = OperationsWorkload.IsOperational(order.Status);
            var orderStudents = students[order.Id].Select(x => x.Student).ToArray();
            var orderAssignments = order.Lines.SelectMany(x => assignments[x.Id]).ToArray();
            var orderShipments = orderStudents.Select(student => shipments.GetValueOrDefault((order.Id, student.Id)))
                .Where(x => x is not null).Select(x => x!).ToArray();
            var active = orderAssignments.Count(x => x.Status == RentalAssignmentStatus.Active &&
                (!returns.TryGetValue(x.Id, out var request) || OperationsWorkload.ReturnState(request) != "completed"));
            var expired = operational && order.Period?.EndDate < today;
            var pending = 0;
            var inTransit = 0;
            var completed = 0;
            var missingForm = 0;
            foreach (var assignment in orderAssignments)
            {
                returns.TryGetValue(assignment.Id, out var request);
                if (request is null && !(expired && assignment.Status == RentalAssignmentStatus.Active)) continue;
                switch (OperationsWorkload.ReturnState(request))
                {
                    case "completed": completed++; break;
                    case "in-transit": inTransit++; break;
                    default: pending++; if (request is null) missingForm++; break;
                }
            }
            rows.Add(new OperationsOrderSummary(order.Id, order.OrderNumber, order.CustomerId,
                customers.GetValueOrDefault(order.CustomerId)?.Name ?? "Müşteri", (int)order.Type,
                (int)order.Status, students[order.Id].FirstOrDefault()?.Name, order.Period?.StartDate,
                order.Period?.EndDate, order.CreatedAt,
                string.Join(", ", order.Lines.Select(x => $"{models.GetValueOrDefault(x.ProductModelId)?.Name ?? "Kit"} × {x.Quantity}")),
                order.Lines.Sum(x => x.Quantity), order.Type == OrderType.Rental ? orderAssignments.Length : order.ProductUnits.Count,
                orderStudents.Length, operational ? orderStudents.Count(x => !x.HasAddress) : 0,
                operational ? orderStudents.Count(x => x.HasAddress &&
                    (!shipments.TryGetValue((order.Id, x.Id), out var shipment) ||
                     shipment.State == KargonomiShipmentState.Failed ||
                     (shipment.State == KargonomiShipmentState.Pending && !shipment.ExternalShipmentId.HasValue))) : 0,
                operational ? orderShipments.Count(x => x.State is KargonomiShipmentState.Draft or KargonomiShipmentState.Ready ||
                    (x.State == KargonomiShipmentState.Pending && x.ExternalShipmentId.HasValue)) : 0,
                operational ? orderShipments.Count(x => x.State == KargonomiShipmentState.InTransit) : 0,
                orderShipments.Count(x => x.State == KargonomiShipmentState.Delivered),
                operational ? orderShipments.Count(x => x.State == KargonomiShipmentState.Failed) : 0,
                active, faultsByOrder[order.Id].Count(x => OperationsWorkload.IsOpenFault(x.Status)),
                pending, inTransit, completed, missingForm, expired && active > 0,
                operational && active > 0 && order.Period?.EndDate >= today && order.Period?.EndDate <= today.AddDays(7)));
        }
        return new Snapshot(rows, customers.Values.OrderBy(x => x.Name)
            .Select(x => new OperationsCustomerOption(x.Id, x.Name)).ToArray(), faults);
    }

    private sealed record Snapshot(IReadOnlyCollection<OperationsOrderSummary> Orders,
        IReadOnlyCollection<OperationsCustomerOption> Customers, IReadOnlyCollection<FaultTicket> Faults);
}
