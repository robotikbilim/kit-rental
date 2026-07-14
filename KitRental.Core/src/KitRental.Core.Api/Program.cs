using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.OpenApi;
using KitRental.Core.Application.Abstractions;
using KitRental.Core.Application.Common;
using KitRental.Core.Application.CustomerPortal;
using KitRental.Core.Application.Inventory;
using KitRental.Core.Application.Operations;
using KitRental.Core.Application.PhysicalKits;
using KitRental.Core.Application.Rentals;
using KitRental.Core.Application.Reporting;
using KitRental.Core.Application.Workshop;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Support;
using KitRental.Core.Infrastructure.Persistence;
using KitRental.Observability;
using KitRental.Security;
using KitRental.SharedKernel;

var builder = WebApplication.CreateBuilder(args);
var tokenOptions = new TokenOptions(
    "KitRental.Identity",
    "KitRental",
    builder.Configuration["Security:TokenSecret"] ?? "development-only-secret-change-before-production-2026",
    TimeSpan.FromHours(8));

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "KitRental Core API",
        Version = "v1",
        Description = "Müşteri, eğitim kiti, komponent stoğu, reçete, üretim, kiralama, kargo, arıza ve iade operasyonları."
    });
    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Identity API'den alınan erişim belirtecini girin."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("bearer", document)] = []
    });
});
builder.Services.AddKitRentalObservability();
builder.Services.AddKitRentalSecurity(tokenOptions);
var useInMemoryPersistence = builder.Environment.IsEnvironment("Testing") ||
    builder.Configuration.GetValue<bool>("Persistence:UseInMemory");
if (useInMemoryPersistence)
{
    builder.Services.AddSingleton<ICoreRepository, InMemoryCoreRepository>();
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("CoreDatabase")
        ?? throw new InvalidOperationException("CoreDatabase bağlantı dizesi tanımlanmalıdır.");
    builder.Services.AddSqlServerPersistence(connectionString);
}
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<RentalAssignmentService>();
builder.Services.AddScoped<OperationsService>();
builder.Services.AddScoped<ReportingService>();
builder.Services.AddScoped<WorkshopService>();
builder.Services.AddScoped<PhysicalKitService>();
builder.Services.AddScoped<CustomerPortalService>();

var app = builder.Build();
if (!useInMemoryPersistence)
{
    await app.Services.MigrateCoreDatabaseAsync();
    if (builder.Configuration.GetValue<bool>("Persistence:SeedDemoData"))
        await app.Services.SeedCoreDemoDataAsync();
}
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "KitRental Core API v1");
    options.DocumentTitle = "KitRental Core API";
    options.DisplayRequestDuration();
    options.EnablePersistAuthorization();
});
app.UseKitRentalObservability();
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, title, code) = exception switch
    {
        ResourceNotFoundException => (StatusCodes.Status404NotFound, "Kayıt bulunamadı", "resource.not_found"),
        ForbiddenException => (StatusCodes.Status403Forbidden, "Bu kayda erişim izniniz yok", "resource.forbidden"),
        ConflictException conflict => (StatusCodes.Status409Conflict, "İş kuralı çakışması", conflict.Code),
        DomainException domain => (StatusCodes.Status400BadRequest, "Geçersiz istek", domain.Code),
        _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen hata", "server.error")
    };
    context.Response.StatusCode = status;
    await Results.Problem(
        statusCode: status,
        title: title,
        detail: exception?.Message,
        extensions: new Dictionary<string, object?> { ["code"] = code }).ExecuteAsync(context);
}));
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api").RequireAuthorization();
var operationsRoles = new[] { "SystemAdmin", "OperationsManager" };
var warehouseRoles = new[] { "SystemAdmin", "OperationsManager", "WarehouseStaff" };
var serviceRoles = new[] { "SystemAdmin", "OperationsManager", "ServiceTechnician" };
var customerRoles = new[] { "CustomerAccountManager", "CustomerUser" };

api.MapGet("/customer-portal", async (ClaimsPrincipal user, CustomerPortalService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetOverviewAsync(GetRequiredCustomerId(user), cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(customerRoles));

api.MapPost("/customer-portal/rental-requests", async (PortalRentalRequest request, ClaimsPrincipal user,
    CustomerPortalService service, CancellationToken cancellationToken) =>
{
    var result = await service.CreateRentalRequestAsync(new CreatePortalRentalRequestCommand(
        GetRequiredCustomerId(user), request.AddressId, request.StartDate, request.EndDate,
        request.Lines.Select(line => new PortalRentalLineCommand(line.ProductModelId, line.Quantity)).ToArray(),
        user.GetRequiredUserId()), cancellationToken);
    return Results.Created($"/api/orders/{result.Id}", result);
}).RequireAuthorization(policy => policy.RequireRole(customerRoles));

api.MapPost("/customer-portal/faults", async (PortalFaultRequest request, ClaimsPrincipal user,
    CustomerPortalService service, CancellationToken cancellationToken) =>
{
    var result = await service.OpenFaultAsync(new OpenPortalFaultCommand(GetRequiredCustomerId(user),
        request.AssignmentId, request.Category, request.Severity, request.Description, user.GetRequiredUserId()),
        cancellationToken);
    return Results.Created($"/api/faults/{result.Id}", result);
}).RequireAuthorization(policy => policy.RequireRole(customerRoles));

api.MapGet("/order-summaries", async (ClaimsPrincipal user, CustomerPortalService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetOrderSummariesAsync(user.GetCustomerId(), cancellationToken)));

api.MapPost("/product-models", async (CreateProductModelRequest request, ClaimsPrincipal user, InventoryService service, CancellationToken cancellationToken) =>
{
    var result = await service.CreateModelAsync(
        new CreateProductModelCommand(request.Name, request.Sku, request.Description, request.ImageUrl, user.GetRequiredUserId()),
        cancellationToken);
    return Results.Created($"/api/product-models/{result.Id}", result);
}).RequireAuthorization(policy => policy.RequireRole(operationsRoles));

api.MapGet("/product-models", async (InventoryService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetModelsAsync(cancellationToken)));

api.MapGet("/product-models/{productModelId:guid}", async (Guid productModelId, InventoryService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetModelAsync(productModelId, cancellationToken)));

api.MapPut("/product-models/{productModelId:guid}", async (Guid productModelId, UpdateProductModelRequest request,
    ClaimsPrincipal user, InventoryService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.UpdateModelAsync(new UpdateProductModelCommand(productModelId, request.Name, request.Sku,
        request.Description, request.ImageUrl, user.GetRequiredUserId()), cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(operationsRoles));

api.MapDelete("/product-models/{productModelId:guid}", async (Guid productModelId, ClaimsPrincipal user,
    InventoryService service, CancellationToken cancellationToken) =>
{
    await service.DeleteModelAsync(productModelId, user.GetRequiredUserId(), cancellationToken);
    return Results.NoContent();
}).RequireAuthorization(policy => policy.RequireRole(operationsRoles));

api.MapPost("/product-units", async (CreateProductUnitRequest request, ClaimsPrincipal user, InventoryService service, CancellationToken cancellationToken) =>
{
    var result = await service.CreateUnitAsync(
        new CreateProductUnitCommand(request.ProductModelId, request.SerialNumber, request.QrCode, user.GetRequiredUserId()),
        cancellationToken);
    return Results.Created($"/api/product-units/{result.Id}", result);
}).RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapPost("/product-units/bulk", async (CreateProductUnitsRequest request, ClaimsPrincipal user, InventoryService service, CancellationToken cancellationToken) =>
{
    var result = await service.CreateUnitsAsync(
        new CreateProductUnitsCommand(request.ProductModelId, request.Quantity, user.GetRequiredUserId()), cancellationToken);
    return Results.Created("/api/product-units", result);
}).RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapGet("/product-units", async (InventoryService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetUnitsAsync(cancellationToken)));

api.MapGet("/inventory", async (string? query, Guid? productModelId, ProductUnitStatus? status,
    DateOnly? createdFrom, DateOnly? createdTo, int? page, int? pageSize, InventoryService service,
    CancellationToken cancellationToken) => Results.Ok(await service.GetInventoryAsync(query, productModelId,
        status, createdFrom, createdTo, page ?? 1, pageSize ?? 20, cancellationToken)));

api.MapPut("/product-units/{id:guid}", async (Guid id, UpdateProductUnitRequest request, ClaimsPrincipal user,
    InventoryService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.UpdateUnitAsync(new UpdateProductUnitCommand(id, request.SerialNumber, request.QrCode,
        user.GetRequiredUserId()), cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapDelete("/product-units/{id:guid}", async (Guid id, ClaimsPrincipal user, InventoryService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteUnitAsync(id, user.GetRequiredUserId(), cancellationToken);
    return Results.NoContent();
}).RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapGet("/physical-kits/dashboard", async (PhysicalKitService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetDashboardAsync(cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapGet("/physical-kits", async (PhysicalKitService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetListAsync(cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapGet("/physical-kits/lookup", async (string identifier, PhysicalKitService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.LookupAsync(identifier, cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapPost("/physical-kits/bulk-rent", async (BulkRentPhysicalKitsRequest request, ClaimsPrincipal user,
    PhysicalKitService service, CancellationToken cancellationToken) =>
    Results.Created("/api/physical-kits", await service.RentManyAsync(new BulkRentPhysicalKitsCommand(
        request.ProductUnitIds, request.CustomerName, request.Email, request.Phone, request.AddressLine,
        request.District, request.City, request.PostalCode, request.StartDate, request.EndDate,
        user.GetRequiredUserId()), cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(operationsRoles));

api.MapGet("/physical-kits/{id:guid}", async (Guid id, PhysicalKitService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetDetailAsync(id, cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapPost("/physical-kits/{id:guid}/rent", async (Guid id, RentPhysicalKitRequest request, ClaimsPrincipal user,
    PhysicalKitService service, CancellationToken cancellationToken) =>
    Results.Created($"/api/physical-kits/{id}", await service.RentAsync(new RentPhysicalKitCommand(id,
        request.CustomerName, request.Email, request.Phone, request.AddressLine, request.District, request.City,
        request.PostalCode, request.StartDate, request.EndDate, user.GetRequiredUserId()), cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(operationsRoles));

api.MapPost("/components", async (CreateComponentRequest request, ClaimsPrincipal user, WorkshopService service, CancellationToken cancellationToken) =>
{
    var result = await service.CreateComponentAsync(
        new CreateComponentCommand(request.Name, request.Sku, request.UnitOfMeasure, request.MinimumStock, request.ImageUrl,
            user.GetRequiredUserId()),
        cancellationToken);
    return Results.Created($"/api/components/{result.Id}", result);
}).RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapGet("/components", async (bool? lowStockOnly, WorkshopService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetComponentsAsync(lowStockOnly ?? false, cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapGet("/components/low-stock", async (WorkshopService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetComponentsAsync(true, cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapGet("/components/search", async (string? query, int? limit, WorkshopService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.SearchComponentsAsync(query, limit ?? 8, cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapGet("/components/{componentId:guid}/locator", async (Guid componentId, WorkshopService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetComponentLocatorAsync(componentId, cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapPost("/storage-locations", async (CreateStorageLocationRequest request, ClaimsPrincipal user, WorkshopService service, CancellationToken cancellationToken) =>
{
    var result = await service.CreateLocationAsync(
        new CreateStorageLocationCommand(request.Code, request.Warehouse, request.Aisle, request.Rack, request.Shelf,
            user.GetRequiredUserId()), cancellationToken);
    return Results.Created($"/api/storage-locations/{result.Id}", result);
}).RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapGet("/storage-locations", async (WorkshopService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetLocationsAsync(cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapPost("/component-stock/receipts", async (RecordComponentStockRequest request, ClaimsPrincipal user, WorkshopService service, CancellationToken cancellationToken) =>
    Results.Created("/api/component-stock/movements", await service.ReceiveAsync(
        new RecordStockCommand(request.ComponentId, request.StorageLocationId, request.Quantity, request.Reference,
            user.GetRequiredUserId()), cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapPost("/component-stock/consumptions", async (RecordComponentStockRequest request, ClaimsPrincipal user, WorkshopService service, CancellationToken cancellationToken) =>
    Results.Created("/api/component-stock/movements", await service.ConsumeAsync(
        new RecordStockCommand(request.ComponentId, request.StorageLocationId, request.Quantity, request.Reference,
            user.GetRequiredUserId()), cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapPost("/component-stock/transfers", async (TransferComponentStockRequest request, ClaimsPrincipal user, WorkshopService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.TransferAsync(
        new TransferStockCommand(request.ComponentId, request.FromStorageLocationId, request.ToStorageLocationId,
            request.Quantity, request.Reference, user.GetRequiredUserId()), cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapGet("/component-stock", async (Guid? componentId, Guid? locationId, WorkshopService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetStocksAsync(componentId, locationId, cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapGet("/component-stock/movements", async (Guid? componentId, WorkshopService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetMovementsAsync(componentId, cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapPost("/product-models/{productModelId:guid}/bom", async (Guid productModelId, CreateBillOfMaterialsRequest request,
    ClaimsPrincipal user, WorkshopService service, CancellationToken cancellationToken) =>
{
    var result = await service.CreateBomAsync(new CreateBillOfMaterialsCommand(productModelId, request.Version,
        request.Lines.Select(line => new BillOfMaterialsLineCommand(line.ComponentId, line.Quantity)).ToArray(),
        user.GetRequiredUserId()), cancellationToken);
    return Results.Created($"/api/product-models/{productModelId}/bom", result);
}).RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapGet("/product-models/{productModelId:guid}/bom", async (Guid productModelId, WorkshopService service, CancellationToken cancellationToken) =>
{
    var bom = await service.GetActiveBomAsync(productModelId, cancellationToken);
    return bom is null ? Results.NoContent() : Results.Ok(bom);
})
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapPut("/components/{componentId:guid}", async (Guid componentId, UpdateComponentRequest request,
    ClaimsPrincipal user, WorkshopService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.UpdateComponentAsync(new UpdateComponentCommand(componentId, request.Name, request.Sku,
        request.UnitOfMeasure, request.MinimumStock, request.ImageUrl, user.GetRequiredUserId()), cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapDelete("/components/{componentId:guid}", async (Guid componentId, ClaimsPrincipal user,
    WorkshopService service, CancellationToken cancellationToken) =>
{
    await service.DeleteComponentAsync(componentId, user.GetRequiredUserId(), cancellationToken);
    return Results.NoContent();
}).RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapGet("/physical-kits/models", async (PhysicalKitService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetModelSummariesAsync(cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapGet("/physical-kits/models/{productModelId:guid}/units", async (Guid productModelId, string? filter,
    int? page, int? pageSize, PhysicalKitService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetModelUnitsAsync(productModelId, filter, page ?? 1, pageSize ?? 20, cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapGet("/physical-kits/models/{productModelId:guid}/labels", async (Guid productModelId, string? filter,
    PhysicalKitService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetModelUnitsForLabelsAsync(productModelId, filter, cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapGet("/manufacturing/buildable-kits", async (WorkshopService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetBuildableKitsAsync(null, cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapGet("/manufacturing/buildable-kits/{productModelId:guid}", async (Guid productModelId, WorkshopService service, CancellationToken cancellationToken) =>
    Results.Ok((await service.GetBuildableKitsAsync(productModelId, cancellationToken)).Single()))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapPost("/kits", async (CreateKitRequest request, ClaimsPrincipal user, WorkshopService service, CancellationToken cancellationToken) =>
{
    var result = await service.CreateKitAsync(new CreateKitCommand(request.Name, request.Sku, request.Description,
        request.ImageUrl, request.BomVersion,
        request.Lines.Select(line => new BillOfMaterialsLineCommand(line.ComponentId, line.Quantity)).ToArray(),
        user.GetRequiredUserId()), cancellationToken);
    return Results.Created($"/api/product-models/{result.Id}", result);
}).RequireAuthorization(policy => policy.RequireRole(operationsRoles));

api.MapPost("/customers", async (CreateCustomerRequest request, ClaimsPrincipal user, OperationsService service, CancellationToken cancellationToken) =>
{
    var result = await service.CreateCustomerAsync(
        new CreateCustomerCommand(request.Name, request.Email,
            new AddressCommand(request.Address.Title, request.Address.ContactName, request.Address.Phone, request.Address.Line1,
                request.Address.District, request.Address.City, request.Address.PostalCode), user.GetRequiredUserId()),
        cancellationToken);
    return Results.Created($"/api/customers/{result.Id}", result);
}).RequireAuthorization(policy => policy.RequireRole(operationsRoles));

api.MapGet("/customers", async (OperationsService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetCustomersAsync(cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(operationsRoles));

api.MapPost("/orders", async (CreateOrderRequest request, ClaimsPrincipal user, OperationsService service, CancellationToken cancellationToken) =>
{
    EnsureCustomerScope(user, request.CustomerId);
    var result = await service.CreateOrderAsync(
        new CreateOrderCommand(request.CustomerId, request.AddressId, request.StartDate, request.EndDate,
            request.Lines.Select(line => new OrderLineCommand(line.ProductModelId, line.Quantity)).ToArray(),
            user.GetRequiredUserId()),
        cancellationToken);
    return Results.Created($"/api/orders/{result.Id}", result);
});

api.MapGet("/orders", async (ClaimsPrincipal user, OperationsService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetOrdersAsync(user.GetCustomerId(), cancellationToken)));

api.MapPost("/orders/{orderId:guid}/transitions", async (Guid orderId, OrderTransitionRequest request, ClaimsPrincipal user, OperationsService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.TransitionOrderAsync(orderId, request.Target, user.GetRequiredUserId(), cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(operationsRoles));

api.MapPost("/rental-assignments", async (CreateRentalAssignmentRequest request, ClaimsPrincipal user, RentalAssignmentService service, CancellationToken cancellationToken) =>
{
    var result = await service.CreateAsync(
        new CreateRentalAssignmentCommand(request.OrderLineId, request.CustomerId, request.ProductUnitId, request.StartDate,
            request.EndDate, user.GetRequiredUserId()), cancellationToken);
    return Results.Created($"/api/rental-assignments/{result.Id}", result);
}).RequireAuthorization(policy => policy.RequireRole(operationsRoles));

api.MapPost("/shipments", async (CreateShipmentRequest request, ClaimsPrincipal user, OperationsService service, CancellationToken cancellationToken) =>
{
    var result = await service.CreateShipmentAsync(
        new CreateShipmentCommand(request.OrderId, request.FaultTicketId, request.Type, request.Carrier, request.TrackingNumber,
            user.GetRequiredUserId()), cancellationToken);
    return Results.Created($"/api/shipments/{result.Id}", result);
}).RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapPost("/shipments/{shipmentId:guid}/events", async (Guid shipmentId, ShipmentEventRequest request, ClaimsPrincipal user, OperationsService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.AddShipmentEventAsync(
        new AddShipmentEventCommand(shipmentId, request.Status, request.OccurredAt, request.Location, request.Description,
            user.GetRequiredUserId()), cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapGet("/orders/{orderId:guid}/shipments", async (Guid orderId, OperationsService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetShipmentsAsync(orderId, cancellationToken)));

api.MapPost("/faults", async (OpenFaultRequest request, ClaimsPrincipal user, OperationsService service, CancellationToken cancellationToken) =>
{
    EnsureCustomerScope(user, request.CustomerId);
    var result = await service.OpenFaultAsync(
        new OpenFaultCommand(request.CustomerId, request.OrderId, request.AssignmentId, request.ProductUnitId,
            request.Category, request.Severity, request.Description, user.GetRequiredUserId()), cancellationToken);
    return Results.Created($"/api/faults/{result.Id}", result);
});

api.MapGet("/faults", async (ClaimsPrincipal user, OperationsService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetFaultTicketsAsync(user.GetCustomerId(), cancellationToken)));

api.MapPost("/faults/{ticketId:guid}/status", async (Guid ticketId, FaultStatusRequest request, ClaimsPrincipal user, OperationsService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ChangeFaultStatusAsync(ticketId, request.Status, user.GetRequiredUserId(), request.Note, cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(serviceRoles));

api.MapPost("/return-inspections", async (CompleteInspectionRequest request, ClaimsPrincipal user, OperationsService service, CancellationToken cancellationToken) =>
{
    var result = await service.CompleteInspectionAsync(
        new CompleteInspectionCommand(request.OrderId, request.ProductUnitId,
            request.Items.Select(item => new InspectionItemCommand(item.Name, item.IsPresent, item.IsDamaged, item.Note)).ToArray(),
            request.DamageCharge, request.Outcome, user.GetRequiredUserId()), cancellationToken);
    return Results.Created($"/api/return-inspections/{result.Id}", result);
}).RequireAuthorization(policy => policy.RequireRole(warehouseRoles));

api.MapGet("/dashboard", async (OperationsService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetDashboardAsync(cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(operationsRoles));

api.MapGet("/audit", async (ReportingService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetAuditTrailAsync(cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole("SystemAdmin", "Auditor"));

api.MapGet("/reports/inventory.csv", async (ReportingService service, CancellationToken cancellationToken) =>
    Results.File(await service.ExportInventoryCsvAsync(cancellationToken), "text/csv; charset=utf-8", "inventory.csv"))
    .RequireAuthorization(policy => policy.RequireRole(operationsRoles));

app.MapHealthChecks("/health");
app.Run();

static void EnsureCustomerScope(ClaimsPrincipal user, Guid requestedCustomerId)
{
    var customerId = user.GetCustomerId();
    if (customerId.HasValue && customerId.Value != requestedCustomerId)
        throw new ForbiddenException("Başka bir müşteri hesabı adına işlem yapılamaz.");
}

static Guid GetRequiredCustomerId(ClaimsPrincipal user) => user.GetCustomerId()
    ?? throw new ForbiddenException("Bu işlem için bir müşteri hesabına bağlı olmalısınız.");

public sealed record CreateProductModelRequest(string Name, string Sku, string? Description = null, string? ImageUrl = null);
public sealed record UpdateProductModelRequest(string Name, string Sku, string? Description = null, string? ImageUrl = null);
public sealed record CreateProductUnitRequest(Guid ProductModelId, string? SerialNumber = null, string? QrCode = null);
public sealed record CreateProductUnitsRequest(Guid ProductModelId, int Quantity);
public sealed record UpdateProductUnitRequest(string SerialNumber, string QrCode);
public sealed record RentPhysicalKitRequest(string CustomerName, string Email, string Phone, string AddressLine,
    string District, string City, string PostalCode, DateOnly StartDate, DateOnly EndDate);
public sealed record BulkRentPhysicalKitsRequest(IReadOnlyCollection<Guid> ProductUnitIds, string CustomerName,
    string Email, string Phone, string AddressLine, string District, string City, string PostalCode,
    DateOnly StartDate, DateOnly EndDate);
public sealed record CreateComponentRequest(string Name, string Sku, string UnitOfMeasure, decimal MinimumStock, string? ImageUrl = null);
public sealed record UpdateComponentRequest(string Name, string Sku, string UnitOfMeasure, decimal MinimumStock, string? ImageUrl = null);
public sealed record CreateStorageLocationRequest(string Code, string Warehouse, string Aisle, string Rack, string Shelf);
public sealed record RecordComponentStockRequest(Guid ComponentId, Guid StorageLocationId, decimal Quantity, string Reference);
public sealed record TransferComponentStockRequest(Guid ComponentId, Guid FromStorageLocationId, Guid ToStorageLocationId, decimal Quantity, string Reference);
public sealed record BillOfMaterialsLineRequest(Guid ComponentId, decimal Quantity);
public sealed record CreateBillOfMaterialsRequest(int Version, IReadOnlyCollection<BillOfMaterialsLineRequest> Lines);
public sealed record CreateKitRequest(string Name, string Sku, string? Description, string? ImageUrl, int BomVersion,
    IReadOnlyCollection<BillOfMaterialsLineRequest> Lines);
public sealed record AddressRequest(string Title, string ContactName, string Phone, string Line1, string District, string City, string PostalCode);
public sealed record CreateCustomerRequest(string Name, string Email, AddressRequest Address);
public sealed record OrderLineRequest(Guid ProductModelId, int Quantity);
public sealed record CreateOrderRequest(Guid CustomerId, Guid AddressId, DateOnly StartDate, DateOnly EndDate, IReadOnlyCollection<OrderLineRequest> Lines);
public sealed record OrderTransitionRequest(RentalOrderStatus Target);
public sealed record CreateRentalAssignmentRequest(Guid OrderLineId, Guid CustomerId, Guid ProductUnitId, DateOnly StartDate, DateOnly EndDate);
public sealed record CreateShipmentRequest(Guid OrderId, Guid? FaultTicketId, ShipmentType Type, string Carrier, string TrackingNumber);
public sealed record ShipmentEventRequest(ShipmentStatus Status, DateTimeOffset OccurredAt, string Location, string Description);
public sealed record OpenFaultRequest(Guid CustomerId, Guid OrderId, Guid AssignmentId, Guid ProductUnitId, string Category, FaultSeverity Severity, string Description);
public sealed record FaultStatusRequest(FaultStatus Status, string Note);
public sealed record InspectionItemRequest(string Name, bool IsPresent, bool IsDamaged, string Note);
public sealed record CompleteInspectionRequest(Guid OrderId, Guid ProductUnitId, IReadOnlyCollection<InspectionItemRequest> Items, decimal DamageCharge, ProductUnitStatus Outcome);
public sealed record PortalRentalRequest(Guid AddressId, DateOnly StartDate, DateOnly EndDate, IReadOnlyCollection<OrderLineRequest> Lines);
public sealed record PortalFaultRequest(Guid AssignmentId, string Category, FaultSeverity Severity, string Description);
public partial class Program;
