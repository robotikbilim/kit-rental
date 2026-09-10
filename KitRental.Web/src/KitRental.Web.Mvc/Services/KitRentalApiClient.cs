using KitRental.Web.Mvc.Models;
using Microsoft.AspNetCore.Authentication;
using System.Net.Http.Headers;

namespace KitRental.Web.Mvc.Services;

public sealed class KitRentalApiClient(HttpClient client, IHttpContextAccessor contextAccessor)
{
    public async Task<LoginApiResponse?> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            "/identity/api/auth/login", new { email, password }, cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<LoginApiResponse>(cancellationToken)
            : null;
    }

    public Task<DashboardViewModel?> GetDashboardAsync(CancellationToken cancellationToken) =>
        GetAsync<DashboardViewModel>("/core/api/dashboard", cancellationToken);

    public Task<ApiCommandResult<KitLocationGeocodingQueueResultViewModel>> UpdateKitLocationsAsync(
        CancellationToken cancellationToken) =>
        PostAsync<KitLocationGeocodingQueueResultViewModel>("/core/api/dashboard/kit-location-geocoding-jobs", new { },
            cancellationToken);

    public async Task<IReadOnlyCollection<ProductUnitViewModel>> GetProductUnitsAsync(CancellationToken cancellationToken) =>
        await GetPagedItemsAsync<ProductUnitViewModel>("/core/api/product-units?pageSize=5000", cancellationToken);

    public Task<InventoryPageViewModel?> GetInventoryAsync(InventoryFilterViewModel filter,
        CancellationToken cancellationToken)
    {
        var parameters = new List<string>
        {
            $"page={Math.Max(1, filter.Page)}",
            $"pageSize={Math.Clamp(filter.PageSize, 10, 100)}"
        };
        if (!string.IsNullOrWhiteSpace(filter.Query))
            parameters.Add($"query={Uri.EscapeDataString(filter.Query.Trim())}");
        if (filter.ProductModelId.HasValue)
            parameters.Add($"productModelId={filter.ProductModelId.Value}");
        if (filter.Status.HasValue)
            parameters.Add($"status={filter.Status.Value}");
        if (filter.CreatedFrom.HasValue)
            parameters.Add($"createdFrom={filter.CreatedFrom.Value:yyyy-MM-dd}");
        if (filter.CreatedTo.HasValue)
            parameters.Add($"createdTo={filter.CreatedTo.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(filter.RentalExpiry))
            parameters.Add($"rentalExpiry={Uri.EscapeDataString(filter.RentalExpiry.Trim())}");
        return GetAsync<InventoryPageViewModel>($"/core/api/inventory?{string.Join('&', parameters)}", cancellationToken);
    }

    public async Task<IReadOnlyCollection<PortalOrderViewModel>> GetOrdersAsync(CancellationToken cancellationToken) =>
        await GetPagedItemsAsync<PortalOrderViewModel>("/core/api/order-summaries?pageSize=5000", cancellationToken);

    public async Task<IReadOnlyCollection<OrderCustomerViewModel>> GetCustomersAsync(
        CancellationToken cancellationToken) =>
        await GetPagedItemsAsync<OrderCustomerViewModel>("/core/api/customers?pageSize=5000", cancellationToken);

    public async Task<IReadOnlyCollection<UserApiResponse>> GetUsersAsync(CancellationToken cancellationToken) =>
        await GetPagedItemsAsync<UserApiResponse>("/identity/api/users?pageSize=5000", cancellationToken);

    public Task<ApiCommandResult<UserApiResponse>> CreateAdminUserAsync(
        CreateAdminUserViewModel model, CancellationToken cancellationToken) =>
        PostAsync<UserApiResponse>("/identity/api/users", new
        {
            model.Email,
            model.DisplayName,
            model.Password,
            role = 1,
            customerId = (Guid?)null
        }, cancellationToken);

    public Task<AuditPageApiResponse?> GetAuditAsync(AuditFilterViewModel filter,
        CancellationToken cancellationToken)
    {
        var parameters = new List<string>
        {
            $"page={Math.Max(1, filter.Page)}",
            $"pageSize={Math.Clamp(filter.PageSize, 10, 100)}"
        };
        if (!string.IsNullOrWhiteSpace(filter.Action))
            parameters.Add($"action={Uri.EscapeDataString(filter.Action.Trim())}");
        if (filter.ActorId.HasValue) parameters.Add($"actorId={filter.ActorId.Value}");
        if (filter.OccurredFrom.HasValue)
            parameters.Add($"occurredFrom={filter.OccurredFrom.Value:yyyy-MM-dd}T00:00:00%2B03:00");
        if (filter.OccurredTo.HasValue)
            parameters.Add($"occurredTo={filter.OccurredTo.Value.AddDays(1):yyyy-MM-dd}T00:00:00%2B03:00");
        return GetAsync<AuditPageApiResponse>($"/core/api/audit-entries?{string.Join('&', parameters)}", cancellationToken);
    }

    public Task<ApiCommandResult<UserApiResponse>> CreateCustomerContactAccountAsync(
        CustomerContactAccountViewModel model, CancellationToken cancellationToken) =>
        PostAsync<UserApiResponse>("/identity/api/users", new
        {
            email = model.Username,
            displayName = $"{model.FirstName.Trim()} {model.LastName.Trim()}",
            model.Password,
            role = 5,
            model.CustomerId
        }, cancellationToken);

    public Task<OrderCustomerViewModel?> GetCustomerAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<OrderCustomerViewModel>($"/core/api/customers/{id}", cancellationToken);

    public Task<ApiCommandResult<OrderCustomerViewModel>> CreateCustomerAsync(CreateCustomerViewModel model,
        CancellationToken cancellationToken) => PostAsync<OrderCustomerViewModel>("/core/api/customers", new
        {
            model.Customer.Name,
            model.Customer.Email,
            address = new
            {
                model.Address.Title,
                model.Address.ContactName,
                model.Address.Phone,
                model.Address.Line1,
                model.Address.PostalCode
            },
            allowedProductModelIds = model.SelectedAllowedProductModelIds
        }, cancellationToken);

    public Task<ApiCommandResult<OrderCustomerViewModel>> UpdateCustomerAsync(CustomerInputViewModel model,
        CancellationToken cancellationToken) => SendAsync<OrderCustomerViewModel>(HttpMethod.Put,
            $"/core/api/customers/{model.Id}",
            new { model.Name, model.Email, model.IsActive, allowedProductModelIds = model.SelectedAllowedProductModelIds },
            cancellationToken);

    public Task<ApiCommandResult<OrderCustomerViewModel>> DeleteCustomerAsync(Guid id,
        CancellationToken cancellationToken) => SendAsync<OrderCustomerViewModel>(HttpMethod.Delete,
            $"/core/api/customers/{id}", null, cancellationToken);

    public Task<ApiCommandResult<PortalAddressViewModel>> CreateCustomerAddressAsync(CustomerAddressInputViewModel model,
        CancellationToken cancellationToken) => PostAsync<PortalAddressViewModel>(
            $"/core/api/customers/{model.CustomerId}/addresses", AddressBody(model), cancellationToken);

    public Task<ApiCommandResult<PortalAddressViewModel>> UpdateCustomerAddressAsync(CustomerAddressInputViewModel model,
        CancellationToken cancellationToken) => SendAsync<PortalAddressViewModel>(HttpMethod.Put,
            $"/core/api/customers/{model.CustomerId}/addresses/{model.Id}", AddressBody(model), cancellationToken);

    public Task<ApiCommandResult<object>> DeleteCustomerAddressAsync(Guid customerId, Guid addressId,
        CancellationToken cancellationToken) => SendAsync<object>(HttpMethod.Delete,
            $"/core/api/customers/{customerId}/addresses/{addressId}", null, cancellationToken);

    private static object AddressBody(CustomerAddressInputViewModel model) => new
    {
        model.Title,
        model.ContactName,
        model.Phone,
        model.Line1,
        model.PostalCode
    };

    public Task<ApiCommandResult<OrderViewModel>> CreateOrderAsync(AdminOrderInputViewModel model,
        CancellationToken cancellationToken) => PostAsync<OrderViewModel>("/core/api/orders", new
        {
            model.CustomerId,
            model.ProductModelId,
            model.StartDate,
            model.EndDate,
            students = model.Students.Select(student => new { student.FullName, student.GuardianPhone }).ToArray()
        }, cancellationToken);

    public Task<ApiCommandResult<OrderViewModel>> CreatePurchaseOrderAsync(PurchaseOrderInputViewModel model,
        CancellationToken cancellationToken) => PostAsync<OrderViewModel>("/core/api/purchase-orders", new
        {
            model.CustomerId,
            model.AddressId,
            lines = model.Lines.Select(line => new { line.ProductModelId, line.Quantity }).ToArray()
        }, cancellationToken);

    public Task<FaultPageViewModel?> GetFaultsAsync(FaultFilterViewModel filter,
        CancellationToken cancellationToken)
    {
        var parameters = new List<string>
        {
            $"page={Math.Max(1, filter.Page)}",
            $"pageSize={Math.Clamp(filter.PageSize, 10, 100)}"
        };
        if (!string.IsNullOrWhiteSpace(filter.Query))
            parameters.Add($"query={Uri.EscapeDataString(filter.Query.Trim())}");
        if (filter.Status.HasValue)
            parameters.Add($"status={filter.Status.Value}");
        if (filter.Severity.HasValue)
            parameters.Add($"severity={filter.Severity.Value}");
        if (filter.OpenedFrom.HasValue)
            parameters.Add($"openedFrom={filter.OpenedFrom.Value:yyyy-MM-dd}");
        if (filter.OpenedTo.HasValue)
            parameters.Add($"openedTo={filter.OpenedTo.Value:yyyy-MM-dd}");
        return GetAsync<FaultPageViewModel>($"/core/api/faults?{string.Join('&', parameters)}", cancellationToken);
    }

    public async Task<IReadOnlyCollection<FaultGuideEntryViewModel>> GetFaultGuideEntriesAsync(
        CancellationToken cancellationToken) =>
        await GetPagedItemsAsync<FaultGuideEntryViewModel>("/core/api/fault-guides?pageSize=5000", cancellationToken);

    public async Task<IReadOnlyCollection<FaultGuideEntryViewModel>> GetPublicFaultGuideEntriesAsync(
        string qrCode, CancellationToken cancellationToken) =>
        await GetPagedItemsAsync<FaultGuideEntryViewModel>(
            $"/core/api/public/fault-guides/{Uri.EscapeDataString(qrCode)}?pageSize=5000", cancellationToken);

    public Task<ApiCommandResult<FaultGuideEntryViewModel>> CreateFaultGuideEntryAsync(
        FaultGuideEntryInputViewModel model, CancellationToken cancellationToken) =>
        PostAsync<FaultGuideEntryViewModel>("/core/api/fault-guides", new
        {
            model.Title,
            model.Problem,
            model.Solution,
            model.DisplayOrder,
            model.IsActive,
            model.ProductModelId
        }, cancellationToken);

    public Task<ApiCommandResult<FaultGuideEntryViewModel>> UpdateFaultGuideEntryAsync(
        FaultGuideEntryInputViewModel model, CancellationToken cancellationToken) =>
        SendAsync<FaultGuideEntryViewModel>(HttpMethod.Put, $"/core/api/fault-guides/{model.Id}", new
        {
            model.Title,
            model.Problem,
            model.Solution,
            model.DisplayOrder,
            model.IsActive,
            model.ProductModelId
        }, cancellationToken);

    public Task<ApiCommandResult<object>> DeleteFaultGuideEntryAsync(Guid id, CancellationToken cancellationToken) =>
        SendAsync<object>(HttpMethod.Delete, $"/core/api/fault-guides/{id}", null, cancellationToken);

    public async Task<IReadOnlyCollection<ComponentSuggestionViewModel>> SearchComponentsAsync(
        string query,
        CancellationToken cancellationToken) =>
        await GetAsync<ComponentSuggestionViewModel[]>(
            $"/core/api/component-suggestions?query={Uri.EscapeDataString(query)}&limit=8", cancellationToken) ?? [];

    public Task<ComponentLocatorViewModel?> GetComponentLocatorAsync(Guid componentId, CancellationToken cancellationToken) =>
        GetAsync<ComponentLocatorViewModel>($"/core/api/components/{componentId}/locator", cancellationToken);

    public async Task<IReadOnlyCollection<ComponentCatalogViewModel>> GetComponentsAsync(CancellationToken cancellationToken) =>
        await GetPagedItemsAsync<ComponentCatalogViewModel>("/core/api/components?pageSize=5000", cancellationToken);

    public async Task<IReadOnlyCollection<SupplyNeedListViewModel>> GetSupplyNeedsAsync(
        CancellationToken cancellationToken) =>
        await GetPagedItemsAsync<SupplyNeedListViewModel>("/core/api/supply-needs?pageSize=5000", cancellationToken);

    public Task<SupplyNeedListViewModel?> GetSupplyNeedAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<SupplyNeedListViewModel>($"/core/api/supply-needs/{id}", cancellationToken);

    public Task<ApiCommandResult<SupplyNeedListViewModel>> RefreshSupplyNeedRecommendationAsync(
        CancellationToken cancellationToken) => PostAsync<SupplyNeedListViewModel>(
        "/core/api/supply-need-recommendation-refreshes", new { }, cancellationToken);

    public Task<ApiCommandResult<SupplyNeedListViewModel>> CreateSupplyNeedAsync(SupplyNeedInputViewModel model,
        CancellationToken cancellationToken) => PostAsync<SupplyNeedListViewModel>("/core/api/supply-needs",
        new { lines = model.Lines.Select(line => new { line.ComponentId, line.Quantity }).ToArray() }, cancellationToken);

    public Task<ApiCommandResult<SupplyNeedListViewModel>> UpdateSupplyNeedAsync(Guid id,
        SupplyNeedInputViewModel model, CancellationToken cancellationToken) => SendAsync<SupplyNeedListViewModel>(
        HttpMethod.Put, $"/core/api/supply-needs/{id}",
        new { lines = model.Lines.Select(line => new { line.ComponentId, line.Quantity }).ToArray() }, cancellationToken);

    public Task<ApiCommandResult<SupplyNeedListViewModel>> CompleteSupplyNeedAsync(Guid id,
        CompleteSupplyNeedViewModel model, CancellationToken cancellationToken) =>
        PostAsync<SupplyNeedListViewModel>($"/core/api/supply-needs/{id}/completions", new
        {
            model.StorageLocationId,
            lines = model.Lines.Select(line => new { line.ComponentId, Quantity = line.SuppliedQuantity }).ToArray()
        }, cancellationToken);

    public Task<ApiCommandResult<SupplyNeedListViewModel>> ApproveSupplyNeedRecommendationAsync(Guid id,
        CancellationToken cancellationToken) => PostAsync<SupplyNeedListViewModel>(
        $"/core/api/supply-needs/{id}/approvals", new { }, cancellationToken);

    public Task<ApiCommandResult<object>> DeleteSupplyNeedAsync(Guid id, CancellationToken cancellationToken) =>
        SendAsync<object>(HttpMethod.Delete, $"/core/api/supply-needs/{id}", null, cancellationToken);

    public async Task<IReadOnlyCollection<StorageLocationViewModel>> GetStorageLocationsAsync(
        CancellationToken cancellationToken) =>
        await GetPagedItemsAsync<StorageLocationViewModel>("/core/api/storage-locations?pageSize=5000", cancellationToken);

    public Task<ApiCommandResult<StorageLocationViewModel>> CreateStorageLocationAsync(
        StorageLocationInputViewModel model, CancellationToken cancellationToken) =>
        PostAsync<StorageLocationViewModel>("/core/api/storage-locations", model, cancellationToken);

    public Task<ApiCommandResult<StorageLocationViewModel>> UpdateStorageLocationAsync(Guid id,
        StorageLocationInputViewModel model, CancellationToken cancellationToken) =>
        SendAsync<StorageLocationViewModel>(HttpMethod.Put, $"/core/api/storage-locations/{id}", model, cancellationToken);

    public Task<ApiCommandResult<object>> DeleteStorageLocationAsync(Guid id, CancellationToken cancellationToken) =>
        SendAsync<object>(HttpMethod.Delete, $"/core/api/storage-locations/{id}", null, cancellationToken);

    public async Task<IReadOnlyCollection<ProductModelCatalogViewModel>> GetProductModelsAsync(CancellationToken cancellationToken) =>
        await GetPagedItemsAsync<ProductModelCatalogViewModel>("/core/api/product-models?pageSize=5000", cancellationToken);

    public Task<ProductModelCatalogViewModel?> GetProductModelAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<ProductModelCatalogViewModel>($"/core/api/product-models/{id}", cancellationToken);

    public Task<BomViewModel?> GetBomAsync(Guid productModelId, CancellationToken cancellationToken) =>
        GetAsync<BomViewModel>($"/core/api/product-models/{productModelId}/bom", cancellationToken);

    public Task<ApiCommandResult<ComponentCatalogViewModel>> CreateComponentAsync(
        CreateComponentViewModel model,
        CancellationToken cancellationToken) =>
        PostAsync<ComponentCatalogViewModel>("/core/api/components", model, cancellationToken);

    public Task<ApiCommandResult<ComponentCatalogViewModel>> UpdateComponentAsync(Guid id, EditComponentViewModel model,
        CancellationToken cancellationToken) => SendAsync<ComponentCatalogViewModel>(HttpMethod.Put, $"/core/api/components/{id}", model, cancellationToken);
    public Task<ApiCommandResult<object>> DeleteComponentAsync(Guid id, CancellationToken cancellationToken) =>
        SendAsync<object>(HttpMethod.Delete, $"/core/api/components/{id}", null, cancellationToken);
    public Task<ApiCommandResult<ComponentLocatorViewModel>> AdjustComponentStockAsync(Guid id, decimal change,
        CancellationToken cancellationToken) => PostAsync<ComponentLocatorViewModel>(
        $"/core/api/components/{id}/stock-adjustments", new { change }, cancellationToken);

    public Task<ApiCommandResult<ProductModelCatalogViewModel>> CreateKitAsync(
        CreateKitViewModel model,
        CancellationToken cancellationToken) =>
        PostAsync<ProductModelCatalogViewModel>("/core/api/kit-models", new
        {
            model.Name,
            model.Sku,
            model.Description,
            model.ImageUrl,
            model.BomVersion,
            lines = model.Lines.Select(line => new { line.ComponentId, line.Quantity }).ToArray()
        }, cancellationToken);

    public Task<ApiCommandResult<BomViewModel>> SaveBomAsync(
        EditRecipeViewModel model,
        CancellationToken cancellationToken) =>
        PostAsync<BomViewModel>($"/core/api/product-models/{model.ProductModelId}/bom", new
        {
            model.Version,
            lines = model.Lines.Select(line => new { line.ComponentId, line.Quantity }).ToArray()
        }, cancellationToken);

    public Task<PhysicalKitDashboardViewModel?> GetPhysicalKitDashboardAsync(CancellationToken cancellationToken) =>
        GetAsync<PhysicalKitDashboardViewModel>("/core/api/physical-kits/dashboard", cancellationToken);

    public async Task<IReadOnlyCollection<PhysicalKitModelSummaryViewModel>> GetPhysicalKitModelSummariesAsync(
        CancellationToken cancellationToken) =>
        await GetPagedItemsAsync<PhysicalKitModelSummaryViewModel>("/core/api/physical-kits/models?pageSize=5000", cancellationToken);

    public Task<PhysicalKitUnitPageViewModel?> GetPhysicalKitUnitsAsync(Guid productModelId, string filter, int page,
        int pageSize, CancellationToken cancellationToken) => GetAsync<PhysicalKitUnitPageViewModel>(
            $"/core/api/physical-kits/models/{productModelId}/units?filter={Uri.EscapeDataString(filter)}&page={page}&pageSize={pageSize}",
            cancellationToken);

    public async Task<IReadOnlyCollection<PhysicalKitListItemViewModel>> GetPhysicalKitLabelsAsync(Guid productModelId,
        string filter, CancellationToken cancellationToken) =>
        await GetPagedItemsAsync<PhysicalKitListItemViewModel>(
            $"/core/api/physical-kits/models/{productModelId}/labels?filter={Uri.EscapeDataString(filter)}&pageSize=5000",
            cancellationToken);

    public Task<PhysicalKitDetailViewModel?> GetPhysicalKitAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<PhysicalKitDetailViewModel>($"/core/api/physical-kits/{id}", cancellationToken);

    public Task<PhysicalKitDetailViewModel?> LookupPhysicalKitAsync(string identifier, CancellationToken cancellationToken) =>
        GetAsync<PhysicalKitDetailViewModel>($"/core/api/physical-kits/lookup?identifier={Uri.EscapeDataString(identifier)}",
            cancellationToken);

    public Task<ApiCommandResult<ProductUnitViewModel[]>> CreatePhysicalKitsAsync(CreatePhysicalKitViewModel model,
        CancellationToken cancellationToken) => PostAsync<ProductUnitViewModel[]>("/core/api/product-unit-batches",
            new { model.ProductModelId, model.Quantity }, cancellationToken);

    public Task<ApiCommandResult<ProductUnitViewModel>> UpdatePhysicalKitAsync(Guid id, EditPhysicalKitViewModel model,
        CancellationToken cancellationToken) => SendAsync<ProductUnitViewModel>(HttpMethod.Put, $"/core/api/product-units/{id}", model, cancellationToken);
    public Task<ApiCommandResult<object>> DeletePhysicalKitAsync(Guid id, CancellationToken cancellationToken) =>
        SendAsync<object>(HttpMethod.Delete, $"/core/api/product-units/{id}", null, cancellationToken);

    public Task<ApiCommandResult<RentPhysicalKitResultViewModel>> RentPhysicalKitAsync(RentPhysicalKitViewModel model,
        CancellationToken cancellationToken) => PostAsync<RentPhysicalKitResultViewModel>(
            $"/core/api/physical-kits/{model.ProductUnitId}/rentals", model, cancellationToken);

    public Task<ApiCommandResult<BulkRentPhysicalKitsResultViewModel>> BulkRentPhysicalKitsAsync(
        BulkRentPhysicalKitsViewModel model, CancellationToken cancellationToken) =>
        PostAsync<BulkRentPhysicalKitsResultViewModel>("/core/api/physical-kit-rental-batches", new
        {
            model.ProductUnitIds,
            model.CustomerName,
            model.Email,
            model.Phone,
            model.AddressLine,
            model.PostalCode,
            model.StartDate,
            model.EndDate
        }, cancellationToken);

    public Task<ApiCommandResult<ProductModelCatalogViewModel>> UpdateKitAsync(Guid id, EditKitViewModel model,
        CancellationToken cancellationToken) => SendAsync<ProductModelCatalogViewModel>(HttpMethod.Put, $"/core/api/product-models/{id}", model, cancellationToken);
    public Task<ApiCommandResult<object>> DeleteKitAsync(Guid id, CancellationToken cancellationToken) =>
        SendAsync<object>(HttpMethod.Delete, $"/core/api/product-models/{id}", null, cancellationToken);

    public Task<CustomerPortalViewModel?> GetCustomerPortalAsync(CancellationToken cancellationToken) =>
        GetAsync<CustomerPortalViewModel>("/core/api/customer-portal", cancellationToken);

    public async Task<IReadOnlyCollection<PortalRentalCohortViewModel>> GetRentalCohortsAsync(
        CancellationToken cancellationToken) =>
        await GetPagedItemsAsync<PortalRentalCohortViewModel>("/core/api/customer-portal/rental-periods?pageSize=5000",
            cancellationToken);

    public Task<ApiCommandResult<PortalRentalCohortViewModel>> CreateRentalCohortAsync(
        RentalCohortInputViewModel model, CancellationToken cancellationToken) =>
        PostAsync<PortalRentalCohortViewModel>("/core/api/customer-portal/rental-periods",
            new { model.Name, model.StartDate, model.EndDate }, cancellationToken);

    public async Task<IReadOnlyCollection<PortalRentalCohortViewModel>> GetCustomerRentalCohortsAsync(Guid customerId,
        CancellationToken cancellationToken) =>
        await GetPagedItemsAsync<PortalRentalCohortViewModel>($"/core/api/customers/{customerId}/rental-periods?pageSize=5000",
            cancellationToken);

    public Task<ApiCommandResult<PortalRentalCohortViewModel>> UpdateRentalCohortAsync(
        RentalCohortInputViewModel model, CancellationToken cancellationToken) =>
        SendAsync<PortalRentalCohortViewModel>(HttpMethod.Put,
            $"/core/api/customer-portal/rental-periods/{model.Id}",
            new { model.Name, model.StartDate, model.EndDate }, cancellationToken);

    public Task<ApiCommandResult<object>> DeleteRentalCohortAsync(Guid cohortId,
        CancellationToken cancellationToken) =>
        SendAsync<object>(HttpMethod.Delete, $"/core/api/customer-portal/rental-periods/{cohortId}",
            null, cancellationToken);

    public Task<ApiCommandResult<PortalRentalCohortStudentViewModel>> CreateRentalCohortStudentAsync(
        RentalCohortStudentInputViewModel model, CancellationToken cancellationToken) =>
        PostAsync<PortalRentalCohortStudentViewModel>(
            $"/core/api/customer-portal/rental-periods/{model.CohortId}/students",
            new { model.FullName, model.GuardianPhone, AddressLine = model.AddressLine ?? string.Empty, model.ProductModelId },
            cancellationToken);

    public Task<ApiCommandResult<PortalRentalCohortStudentViewModel>> UpdateRentalCohortStudentAsync(
        RentalCohortStudentInputViewModel model, CancellationToken cancellationToken) =>
        SendAsync<PortalRentalCohortStudentViewModel>(HttpMethod.Put,
            $"/core/api/customer-portal/rental-periods/{model.CohortId}/students/{model.Id}",
            new { model.FullName, model.GuardianPhone, AddressLine = model.AddressLine ?? string.Empty, model.ProductModelId },
            cancellationToken);

    public Task<ApiCommandResult<object>> DeleteRentalCohortStudentAsync(Guid cohortId, Guid studentId,
        CancellationToken cancellationToken) =>
        SendAsync<object>(HttpMethod.Delete,
            $"/core/api/customer-portal/rental-periods/{cohortId}/students/{studentId}", null,
            cancellationToken);

    public Task<ApiCommandResult<PortalRentalCohortViewModel>> ImportRentalCohortStudentsAsync(Guid cohortId,
        IReadOnlyCollection<object> rows, CancellationToken cancellationToken) =>
        PostAsync<PortalRentalCohortViewModel>(
            $"/core/api/customer-portal/rental-periods/{cohortId}/student-imports", new { rows },
            cancellationToken);

    public Task<ApiCommandResult<PortalKitReturnViewModel>> CreateStudentReturnAsync(PortalStudentReturnFormViewModel model,
        CancellationToken cancellationToken) =>
        PostAsync<PortalKitReturnViewModel>(
            $"/core/api/customer-portal/rental-periods/{model.CohortId}/students/{model.StudentId}/returns",
            new
            {
                model.RequesterName,
                model.RequesterPhone,
                model.ReturnAddress,
                model.ReturnReason
            },
            cancellationToken);

    public Task<ApiCommandResult<OrderViewModel>> CreateRentalCohortOrderAsync(Guid cohortId,
        CancellationToken cancellationToken) =>
        PostAsync<OrderViewModel>($"/core/api/customer-portal/rental-periods/{cohortId}/orders", new { },
            cancellationToken);

    public Task<ApiCommandResult<FaultViewModel>> CreatePortalFaultAsync(PortalFaultRequestViewModel model,
        CancellationToken cancellationToken) => PostAsync<FaultViewModel>("/core/api/customer-portal/faults",
            new
            {
                model.AssignmentId,
                model.ReporterName,
                model.ReporterPhone,
                model.ReporterAddress,
                model.Description
            }, cancellationToken);

    public Task<PublicFaultKitViewModel?> GetPublicFaultKitAsync(string qrCode, CancellationToken cancellationToken) =>
        GetAsync<PublicFaultKitViewModel>($"/core/api/public/faults/kit/{Uri.EscapeDataString(qrCode)}", cancellationToken);

    public Task<PublicFormAccessTokenViewModel?> CreatePublicFormAccessTokenAsync(string qrCode,
        CancellationToken cancellationToken) =>
        PostRawAsync<PublicFormAccessTokenViewModel>(
            $"/core/api/public/form-access/{Uri.EscapeDataString(qrCode)}", new { }, cancellationToken);

    public Task<PublicFaultContextViewModel?> GetPublicFaultContextAsync(string qrCode, CancellationToken cancellationToken) =>
        GetAsync<PublicFaultContextViewModel>($"/core/api/public/faults/context/{Uri.EscapeDataString(qrCode)}", cancellationToken);

    public Task<PublicKitDeliveryContextViewModel?> GetPublicKitDeliveryContextAsync(string qrCode,
        CancellationToken cancellationToken) =>
        GetAsync<PublicKitDeliveryContextViewModel>(
            $"/core/api/public/deliveries/context/{Uri.EscapeDataString(qrCode)}", cancellationToken);

    public Task<PublicKitReturnContextViewModel?> GetPublicKitReturnContextAsync(string qrCode,
        CancellationToken cancellationToken) =>
        GetAsync<PublicKitReturnContextViewModel>(
            $"/core/api/public/returns/context/{Uri.EscapeDataString(qrCode)}", cancellationToken);

    public Task<ApiCommandResult<object>> CreatePublicFaultAsync(PublicFaultFormViewModel model,
        CancellationToken cancellationToken) => PostAsync<object>("/core/api/public/faults", new
        {
            model.FaultId,
            token = model.AccessToken,
            model.ReporterName,
            model.ReporterPhone,
            model.ReporterAddress,
            model.Description,
            model.Latitude,
            model.Longitude,
            model.AttachmentUrl
        }, cancellationToken);

    public Task<ApiCommandResult<PortalKitReturnViewModel>> CreatePublicReturnAsync(PublicReturnFormViewModel model,
        CancellationToken cancellationToken) => PostAsync<PortalKitReturnViewModel>("/core/api/public/returns", new
        {
            token = model.AccessToken,
            model.RequesterName,
            model.RequesterPhone,
            model.ReturnAddress,
            model.ReturnReason,
            model.DeliveryMethod,
            model.Latitude,
            model.Longitude
        }, cancellationToken);

    public Task<ApiCommandResult<object>> CreatePublicDeliveryAsync(PublicDeliveryFormViewModel model,
        CancellationToken cancellationToken) => PostAsync<object>("/core/api/public/deliveries", new
        {
            token = model.AccessToken,
            model.RecipientName,
            model.RecipientPhone,
            model.AddressLine,
            model.Latitude,
            model.Longitude
        }, cancellationToken);

    public Task<PublicStudentAddressContextViewModel?> GetPublicStudentAddressContextAsync(string token,
        CancellationToken cancellationToken) =>
        GetAsync<PublicStudentAddressContextViewModel>(
            $"/core/api/public/student-addresses/{Uri.EscapeDataString(token)}", cancellationToken);

    public Task<ApiCommandResult<object>> SavePublicStudentAddressAsync(PublicStudentAddressFormViewModel model,
        CancellationToken cancellationToken) =>
        PostAsync<object>($"/core/api/public/student-addresses/{Uri.EscapeDataString(model.Token)}", new
        {
            model.AddressLine,
            model.Latitude,
            model.Longitude
        }, cancellationToken);

    public Task<ApiCommandResult<OrderViewModel>> ConfirmPortalOrderDeliveryAsync(Guid orderId,
        CancellationToken cancellationToken) =>
        PostAsync<OrderViewModel>($"/core/api/customer-portal/orders/{orderId}/delivery-confirmations", new { }, cancellationToken);

    public Task<ApiCommandResult<PortalKitReturnViewModel>> ReceiveKitReturnAsync(Guid returnId,
        CancellationToken cancellationToken) => PostAsync<PortalKitReturnViewModel>(
            $"/core/api/kit-returns/{returnId}/receipts", new { }, cancellationToken);

    public Task<ApiCommandResult<OrderViewModel>> UpdateOrderStatusAsync(Guid orderId, int target,
        CancellationToken cancellationToken) =>
        PostAsync<OrderViewModel>($"/core/api/orders/{orderId}/status-transitions", new { target }, cancellationToken);

    public Task<OrderDetailViewModel?> GetOrderDetailAsync(Guid orderId, CancellationToken cancellationToken) =>
        GetAsync<OrderDetailViewModel>($"/core/api/orders/{orderId}", cancellationToken);

    public Task<ApiCommandResult<OrderKitPreparationViewModel>> CreateOrderKitsAsync(Guid orderId,
        IReadOnlyCollection<PortalRentalLineInputViewModel> lines,
        bool useAvailableKits,
        Guid? rentalCohortId,
        CancellationToken cancellationToken) =>
        PostAsync<OrderKitPreparationViewModel>($"/core/api/orders/{orderId}/kits", new
        {
            lines = lines.Select(line => new { line.ProductModelId, line.Quantity }).ToArray(),
            useAvailableKits,
            rentalCohortId
        }, cancellationToken);

    public Task<ApiCommandResult<FaultViewModel>> ChangeFaultStatusAsync(Guid faultId, int status, string note,
        CancellationToken cancellationToken) => PostAsync<FaultViewModel>($"/core/api/faults/{faultId}/status-events",
            new { status, note }, cancellationToken);

    public async Task<IReadOnlyCollection<EmailDeliveryViewModel>> GetEmailDeliveriesAsync(
        CancellationToken cancellationToken) =>
        await GetPagedItemsAsync<EmailDeliveryViewModel>("/core/api/email-deliveries?pageSize=500", cancellationToken);

    public async Task<IReadOnlyCollection<BuildableKitViewModel>> GetBuildableKitsAsync(CancellationToken cancellationToken) =>
        await GetPagedItemsAsync<BuildableKitViewModel>("/core/api/manufacturing/buildable-kits?pageSize=5000", cancellationToken);

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        var context = contextAccessor.HttpContext;
        var token = context is null ? null : await context.GetTokenAsync("access_token");
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return default;
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            return default;
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    private async Task<IReadOnlyCollection<T>> GetPagedItemsAsync<T>(string path, CancellationToken cancellationToken)
    {
        var result = await GetAsync<PagedApiResponse<T>>(path, cancellationToken);
        return result?.Items ?? [];
    }

    private Task<ApiCommandResult<T>> PostAsync<T>(string path, object body, CancellationToken cancellationToken) =>
        SendAsync<T>(HttpMethod.Post, path, body, cancellationToken);

    private async Task<T?> PostRawAsync<T>(string path, object body, CancellationToken cancellationToken)
    {
        var result = await SendAsync<T>(HttpMethod.Post, path, body, cancellationToken);
        return result.IsSuccess ? result.Data : default;
    }

    private async Task<ApiCommandResult<T>> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        await AddAuthorizationAsync(request);
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var data = response.StatusCode == System.Net.HttpStatusCode.NoContent
                ? default : await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            return new ApiCommandResult<T>(true, data, null);
        }
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(cancellationToken);
        return new ApiCommandResult<T>(false, default, problem?.Detail ?? "İşlem tamamlanamadı.");
    }

    private async Task AddAuthorizationAsync(HttpRequestMessage request)
    {
        var context = contextAccessor.HttpContext;
        var token = context is null ? null : await context.GetTokenAsync("access_token");
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}





