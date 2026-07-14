using System.ComponentModel.DataAnnotations;

namespace KitRental.Web.Mvc.Models;

public sealed class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    public string? Error { get; set; }
}

public sealed record LoginApiResponse(string AccessToken, DateTimeOffset ExpiresAt, UserApiResponse User);
public sealed record UserApiResponse(Guid Id, string Email, string DisplayName, int Role, Guid? CustomerId);
public sealed record DashboardViewModel(int Customers, int ProductUnits, int ActiveOrders, int OpenFaults, int UnitsInMaintenance);
public sealed record ProductUnitViewModel(Guid Id, Guid ProductModelId, string SerialNumber, string QrCode, int Status);
public sealed record OrderViewModel(Guid Id, string OrderNumber, Guid CustomerId, PeriodViewModel Period, int Status, IReadOnlyCollection<OrderLineViewModel> Lines);
public sealed record PeriodViewModel(DateOnly StartDate, DateOnly EndDate);
public sealed record OrderLineViewModel(Guid Id, Guid ProductModelId, int Quantity);
public sealed record FaultViewModel(Guid Id, string Number, Guid CustomerId, string Category, int Severity, string Description, int Status, DateTimeOffset OpenedAt);
public sealed record ComponentSuggestionViewModel(Guid Id, string Name, string Sku, string? ImageUrl, decimal TotalStock, string UnitOfMeasure);
public sealed record ComponentLocationViewModel(Guid StorageLocationId, string LocationCode, string Warehouse, string Aisle, string Rack, string Shelf, decimal Quantity);
public sealed record ComponentLocatorViewModel(
    Guid Id,
    string Name,
    string Sku,
    string UnitOfMeasure,
    string? ImageUrl,
    decimal TotalStock,
    decimal MinimumStock,
    bool IsLowStock,
    IReadOnlyCollection<ComponentLocationViewModel> Locations);

public sealed record ProductModelCatalogViewModel(Guid Id, string Name, string Sku, string? Description, string? ImageUrl);
public sealed record ComponentCatalogViewModel(
    Guid Id, string Name, string Sku, string UnitOfMeasure, decimal MinimumStock, string? ImageUrl,
    decimal TotalStock, bool IsLowStock);
public sealed record BomLineViewModel(Guid ComponentId, string ComponentName, string ComponentSku, decimal Quantity, string UnitOfMeasure);
public sealed record BomViewModel(Guid Id, Guid ProductModelId, string ProductName, string ProductSku, int Version,
    IReadOnlyCollection<BomLineViewModel> Lines);

public sealed class CreateComponentViewModel
{
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(80)] public string Sku { get; set; } = string.Empty;
    [Required, StringLength(40), Display(Name = "Ölçü birimi")] public string UnitOfMeasure { get; set; } = "adet";
    [Range(0, 999999), Display(Name = "Minimum stok")] public decimal MinimumStock { get; set; }
    [Url, Display(Name = "Görsel adresi")] public string? ImageUrl { get; set; }
}

public sealed class CreateKitViewModel
{
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(80)] public string Sku { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    [Url, Display(Name = "Görsel adresi")] public string? ImageUrl { get; set; }
    [Range(1, 999), Display(Name = "Reçete sürümü")] public int BomVersion { get; set; } = 1;
    public List<CreateKitBomLineViewModel> Lines { get; set; } = [];
}

public sealed class CreateKitBomLineViewModel
{
    [Required] public Guid ComponentId { get; set; }
    [Range(0.001, 999999)] public decimal Quantity { get; set; } = 1;
}

public sealed record CreateKitPageViewModel(CreateKitViewModel Form, IReadOnlyCollection<ComponentCatalogViewModel> Components);
public sealed record KitDetailPageViewModel(ProductModelCatalogViewModel Kit, BomViewModel? Bom);
public sealed class EditRecipeViewModel
{
    public Guid ProductModelId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    [Range(1, 999), Display(Name = "Reçete sürümü")] public int Version { get; set; } = 1;
    public List<CreateKitBomLineViewModel> Lines { get; set; } = [];
}
public sealed record EditRecipePageViewModel(EditRecipeViewModel Form,
    IReadOnlyCollection<ComponentCatalogViewModel> Components, bool HasExistingRecipe);
public sealed record ApiCommandResult<T>(bool IsSuccess, T? Data, string? Error);

public sealed record PhysicalKitCurrentRentalViewModel(string CustomerName, string City, DateOnly StartDate, DateOnly EndDate);
public sealed record PhysicalKitListItemViewModel(Guid Id, Guid ProductModelId, string KitName, string KitSku,
    string? ImageUrl, string SerialNumber, string QrCode, int Status, PhysicalKitCurrentRentalViewModel? CurrentRental);
public sealed record PhysicalKitDashboardViewModel(int Total, int Available, int Rented, int Reserved, int InTransit,
    int ServiceOrInspection, IReadOnlyCollection<PhysicalKitListItemViewModel> AvailableKits,
    IReadOnlyCollection<PhysicalKitListItemViewModel> RentedKits, IReadOnlyCollection<PhysicalKitListItemViewModel> AllKits);
public sealed record PhysicalKitStatusEventViewModel(int? PreviousStatus, int NewStatus, DateTimeOffset OccurredAt, string Reason);
public sealed record PhysicalKitRentalHistoryViewModel(Guid AssignmentId, string OrderNumber, int OrderStatus,
    int AssignmentStatus, string CustomerName, string CustomerEmail, string Address, DateOnly StartDate,
    DateOnly EndDate, DateTimeOffset CreatedAt);
public sealed record PhysicalKitFaultHistoryViewModel(string Number, string Category, int Severity, int Status,
    string Description, DateTimeOffset OpenedAt, IReadOnlyCollection<string> StatusNotes);
public sealed record PhysicalKitDetailViewModel(PhysicalKitListItemViewModel Kit,
    IReadOnlyCollection<PhysicalKitRentalHistoryViewModel> RentalHistory,
    IReadOnlyCollection<PhysicalKitFaultHistoryViewModel> FaultHistory,
    IReadOnlyCollection<PhysicalKitStatusEventViewModel> StatusHistory);
public sealed record PhysicalKitLookupPageViewModel(string Identifier, bool HasSearched,
    PhysicalKitDetailViewModel? Result, string? Error);

public sealed class CreatePhysicalKitViewModel
{
    [Required, Display(Name = "Eğitim kiti")] public Guid ProductModelId { get; set; }
    [Range(1, 200), Display(Name = "Oluşturulacak kit adedi")] public int Quantity { get; set; } = 1;
}
public sealed record CreatePhysicalKitPageViewModel(CreatePhysicalKitViewModel Form,
    IReadOnlyCollection<ProductModelCatalogViewModel> KitModels);
public sealed record PhysicalKitLabelViewModel(Guid Id, string KitName, string KitSku, string SerialNumber, string QrCode);
public sealed record PhysicalKitLabelsPageViewModel(DateTimeOffset CreatedAt,
    IReadOnlyCollection<PhysicalKitLabelViewModel> Labels);
public sealed class RentPhysicalKitViewModel
{
    public Guid ProductUnitId { get; set; }
    public string KitName { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    [Required, Display(Name = "Kiralayan kişi / kurum")] public string CustomerName { get; set; } = string.Empty;
    [Required, EmailAddress, Display(Name = "E-posta")] public string Email { get; set; } = string.Empty;
    [Required, Phone, Display(Name = "Telefon")] public string Phone { get; set; } = string.Empty;
    [Required, Display(Name = "Açık adres")] public string AddressLine { get; set; } = string.Empty;
    [Required, Display(Name = "İlçe")] public string District { get; set; } = string.Empty;
    [Required, Display(Name = "Şehir")] public string City { get; set; } = string.Empty;
    [Display(Name = "Posta kodu")] public string PostalCode { get; set; } = string.Empty;
    [Required, DataType(DataType.Date), Display(Name = "Başlangıç")] public DateOnly StartDate { get; set; }
    [Required, DataType(DataType.Date), Display(Name = "Bitiş")] public DateOnly EndDate { get; set; }
}
public sealed record RentPhysicalKitResultViewModel(Guid ProductUnitId, Guid CustomerId, Guid OrderId,
    Guid AssignmentId, string OrderNumber, string SerialNumber, int Status);

public sealed record PortalAddressViewModel(Guid Id, string Title, string ContactName, string Phone, string Line1,
    string District, string City, string PostalCode);
public sealed record PortalProductModelViewModel(Guid Id, string Name, string Sku, string? Description, string? ImageUrl);
public sealed record PortalKitViewModel(Guid ProductUnitId, Guid AssignmentId, Guid OrderId, string OrderNumber,
    string KitName, string KitSku, string? ImageUrl, string SerialNumber, int UnitStatus, int AssignmentStatus,
    DateOnly StartDate, DateOnly EndDate, int OpenFaultCount);
public sealed record PortalOrderLineViewModel(Guid ProductModelId, string ProductName, string ProductSku, int Quantity);
public sealed record PortalOrderViewModel(Guid Id, string OrderNumber, Guid CustomerId, string CustomerName, int Status,
    DateOnly StartDate, DateOnly EndDate, DateTimeOffset CreatedAt, IReadOnlyCollection<PortalOrderLineViewModel> Lines);
public sealed record PortalFaultStatusViewModel(int Previous, int Current, DateTimeOffset OccurredAt, string Note);
public sealed record PortalShipmentEventViewModel(int Status, DateTimeOffset OccurredAt, string Location, string Description);
public sealed record PortalShipmentViewModel(int Type, string Carrier, string TrackingNumber, int Status,
    IReadOnlyCollection<PortalShipmentEventViewModel> Events);
public sealed record PortalFaultViewModel(Guid Id, string Number, Guid ProductUnitId, string KitName, string SerialNumber,
    string Category, int Severity, string Description, int Status, DateTimeOffset OpenedAt,
    IReadOnlyCollection<PortalFaultStatusViewModel> History, IReadOnlyCollection<PortalShipmentViewModel> Shipments);
public sealed record CustomerPortalViewModel(string CustomerName, string CustomerEmail, int ActiveKitCount,
    int PendingRequestCount, int OpenFaultCount, IReadOnlyCollection<PortalKitViewModel> Kits,
    IReadOnlyCollection<PortalOrderViewModel> Orders, IReadOnlyCollection<PortalFaultViewModel> Faults,
    IReadOnlyCollection<PortalAddressViewModel> Addresses, IReadOnlyCollection<PortalProductModelViewModel> ProductModels);

public sealed class PortalRentalRequestViewModel
{
    [Required, Display(Name = "Teslimat adresi")] public Guid AddressId { get; set; }
    [Required, DataType(DataType.Date), Display(Name = "Başlangıç tarihi")] public DateOnly StartDate { get; set; }
    [Required, DataType(DataType.Date), Display(Name = "Bitiş tarihi")] public DateOnly EndDate { get; set; }
    public List<PortalRentalLineInputViewModel> Lines { get; set; } = [new()];
}
public sealed class PortalRentalLineInputViewModel
{
    [Required, Display(Name = "Eğitim kiti")] public Guid ProductModelId { get; set; }
    [Range(1, 100), Display(Name = "Adet")] public int Quantity { get; set; } = 1;
}
public sealed record PortalRentalRequestPageViewModel(PortalRentalRequestViewModel Form,
    IReadOnlyCollection<PortalAddressViewModel> Addresses, IReadOnlyCollection<PortalProductModelViewModel> ProductModels);

public sealed class PortalFaultRequestViewModel
{
    [Required] public Guid AssignmentId { get; set; }
    [Required, StringLength(160), Display(Name = "Arıza kategorisi")] public string Category { get; set; } = string.Empty;
    [Range(1, 4), Display(Name = "Önem derecesi")] public int Severity { get; set; } = 2;
    [Required, StringLength(4000), Display(Name = "Arıza açıklaması")] public string Description { get; set; } = string.Empty;
}
public sealed record PortalFaultRequestPageViewModel(PortalFaultRequestViewModel Form,
    IReadOnlyCollection<PortalKitViewModel> ActiveKits);

public sealed record BuildableComponentViewModel(Guid ComponentId, string ComponentName, string ComponentSku,
    string UnitOfMeasure, string? ImageUrl, decimal RequiredPerKit, decimal AvailableStock,
    int SupportsKitCount, bool IsBottleneck, bool IsLowStock, decimal MissingForNextKit);
public sealed record BuildableKitViewModel(Guid ProductModelId, string ProductName, string ProductSku,
    string? ProductImageUrl, int BomVersion, int BuildableQuantity,
    IReadOnlyCollection<BuildableComponentViewModel> Components);
public sealed record ManufacturingDashboardViewModel(IReadOnlyCollection<BuildableKitViewModel> Kits)
{
    public int TotalBuildable => Kits.Sum(item => item.BuildableQuantity);
    public int BuildableModelCount => Kits.Count(item => item.BuildableQuantity > 0);
    public int BlockedModelCount => Kits.Count(item => item.BuildableQuantity == 0);
    public int LowStockComponentCount => Kits.SelectMany(item => item.Components)
        .Where(item => item.IsLowStock).Select(item => item.ComponentId).Distinct().Count();
}
