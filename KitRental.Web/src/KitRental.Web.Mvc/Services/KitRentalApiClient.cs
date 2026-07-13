using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using KitRental.Web.Mvc.Models;

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

    public async Task<IReadOnlyCollection<ProductUnitViewModel>> GetProductUnitsAsync(CancellationToken cancellationToken) =>
        await GetAsync<ProductUnitViewModel[]>("/core/api/product-units", cancellationToken) ?? [];

    public async Task<IReadOnlyCollection<PortalOrderViewModel>> GetOrdersAsync(CancellationToken cancellationToken) =>
        await GetAsync<PortalOrderViewModel[]>("/core/api/order-summaries", cancellationToken) ?? [];

    public async Task<IReadOnlyCollection<FaultViewModel>> GetFaultsAsync(CancellationToken cancellationToken) =>
        await GetAsync<FaultViewModel[]>("/core/api/faults", cancellationToken) ?? [];

    public async Task<IReadOnlyCollection<ComponentSuggestionViewModel>> SearchComponentsAsync(
        string query,
        CancellationToken cancellationToken) =>
        await GetAsync<ComponentSuggestionViewModel[]>(
            $"/core/api/components/search?query={Uri.EscapeDataString(query)}&limit=8", cancellationToken) ?? [];

    public Task<ComponentLocatorViewModel?> GetComponentLocatorAsync(Guid componentId, CancellationToken cancellationToken) =>
        GetAsync<ComponentLocatorViewModel>($"/core/api/components/{componentId}/locator", cancellationToken);

    public async Task<IReadOnlyCollection<ComponentCatalogViewModel>> GetComponentsAsync(CancellationToken cancellationToken) =>
        await GetAsync<ComponentCatalogViewModel[]>("/core/api/components", cancellationToken) ?? [];

    public async Task<IReadOnlyCollection<ProductModelCatalogViewModel>> GetProductModelsAsync(CancellationToken cancellationToken) =>
        await GetAsync<ProductModelCatalogViewModel[]>("/core/api/product-models", cancellationToken) ?? [];

    public Task<ProductModelCatalogViewModel?> GetProductModelAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<ProductModelCatalogViewModel>($"/core/api/product-models/{id}", cancellationToken);

    public Task<BomViewModel?> GetBomAsync(Guid productModelId, CancellationToken cancellationToken) =>
        GetAsync<BomViewModel>($"/core/api/product-models/{productModelId}/bom", cancellationToken);

    public Task<ApiCommandResult<ComponentCatalogViewModel>> CreateComponentAsync(
        CreateComponentViewModel model,
        CancellationToken cancellationToken) =>
        PostAsync<ComponentCatalogViewModel>("/core/api/components", model, cancellationToken);

    public Task<ApiCommandResult<ProductModelCatalogViewModel>> CreateKitAsync(
        CreateKitViewModel model,
        CancellationToken cancellationToken) =>
        PostAsync<ProductModelCatalogViewModel>("/core/api/kits", new
        {
            model.Name,
            model.Sku,
            model.Description,
            model.ImageUrl,
            model.BomVersion,
            lines = model.Lines.Select(line => new { line.ComponentId, line.Quantity }).ToArray()
        }, cancellationToken);

    public Task<PhysicalKitDashboardViewModel?> GetPhysicalKitDashboardAsync(CancellationToken cancellationToken) =>
        GetAsync<PhysicalKitDashboardViewModel>("/core/api/physical-kits/dashboard", cancellationToken);

    public Task<PhysicalKitDetailViewModel?> GetPhysicalKitAsync(Guid id, CancellationToken cancellationToken) =>
        GetAsync<PhysicalKitDetailViewModel>($"/core/api/physical-kits/{id}", cancellationToken);

    public Task<ApiCommandResult<ProductUnitViewModel>> CreatePhysicalKitAsync(CreatePhysicalKitViewModel model,
        CancellationToken cancellationToken) => PostAsync<ProductUnitViewModel>("/core/api/product-units", model, cancellationToken);

    public Task<ApiCommandResult<RentPhysicalKitResultViewModel>> RentPhysicalKitAsync(RentPhysicalKitViewModel model,
        CancellationToken cancellationToken) => PostAsync<RentPhysicalKitResultViewModel>(
            $"/core/api/physical-kits/{model.ProductUnitId}/rent", model, cancellationToken);

    public Task<CustomerPortalViewModel?> GetCustomerPortalAsync(CancellationToken cancellationToken) =>
        GetAsync<CustomerPortalViewModel>("/core/api/customer-portal", cancellationToken);

    public Task<ApiCommandResult<OrderViewModel>> CreatePortalRentalRequestAsync(PortalRentalRequestViewModel model,
        CancellationToken cancellationToken) => PostAsync<OrderViewModel>("/core/api/customer-portal/rental-requests",
            new { model.AddressId, model.StartDate, model.EndDate, lines = model.Lines }, cancellationToken);

    public Task<ApiCommandResult<FaultViewModel>> CreatePortalFaultAsync(PortalFaultRequestViewModel model,
        CancellationToken cancellationToken) => PostAsync<FaultViewModel>("/core/api/customer-portal/faults",
            new { model.AssignmentId, model.Category, model.Severity, model.Description }, cancellationToken);

    public Task<ApiCommandResult<OrderViewModel>> ApproveOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        PostAsync<OrderViewModel>($"/core/api/orders/{orderId}/transitions", new { target = 3 }, cancellationToken);

    public Task<ApiCommandResult<FaultViewModel>> ChangeFaultStatusAsync(Guid faultId, int status, string note,
        CancellationToken cancellationToken) => PostAsync<FaultViewModel>($"/core/api/faults/{faultId}/status",
            new { status, note }, cancellationToken);

    public async Task<IReadOnlyCollection<BuildableKitViewModel>> GetBuildableKitsAsync(CancellationToken cancellationToken) =>
        await GetAsync<BuildableKitViewModel[]>("/core/api/manufacturing/buildable-kits", cancellationToken) ?? [];

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
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    private async Task<ApiCommandResult<T>> PostAsync<T>(string path, object body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        await AddAuthorizationAsync(request);
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return new ApiCommandResult<T>(true, await response.Content.ReadFromJsonAsync<T>(cancellationToken), null);
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
