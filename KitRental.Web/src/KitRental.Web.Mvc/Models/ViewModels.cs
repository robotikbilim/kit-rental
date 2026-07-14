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
public sealed record UserApiResponse(Guid Id, string Email, string DisplayName, int Role, Guid? CustomerId, bool IsActive = true);
public sealed record DashboardViewModel(
    int Customers,
    int ProductUnits,
    int RentedKits,
    int AvailableKits,
    int FaultyKits,
    int RepairedAwaitingShipment,
    int PreparingKits,
    int KitsInTransit,
    int KitsUnderInspection,
    int UnitsInMaintenance,
    int ActiveOrders,
    int OrdersAwaitingApproval,
    int OverdueOrders);
public sealed record ProductUnitViewModel(Guid Id, Guid ProductModelId, string SerialNumber, string QrCode, int Status);
public sealed record InventoryItemViewModel(Guid Id, Guid ProductModelId, string ProductModelName,
    string ProductModelSku, string SerialNumber, string QrCode, int Status, DateTimeOffset CreatedAt);
public sealed record InventoryPageViewModel(int Page, int PageSize, int TotalCount, int TotalPages,
    IReadOnlyCollection<InventoryItemViewModel> Items);
public sealed class InventoryFilterViewModel
{
    public string? Query { get; set; }
    public Guid? ProductModelId { get; set; }
    public int? Status { get; set; }
    [DataType(DataType.Date)] public DateOnly? CreatedFrom { get; set; }
    [DataType(DataType.Date)] public DateOnly? CreatedTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
public sealed record InventoryScreenViewModel(InventoryPageViewModel Result, InventoryFilterViewModel Filter,
    IReadOnlyCollection<ProductModelCatalogViewModel> ProductModels);
public sealed record OrderViewModel(Guid Id, string OrderNumber, Guid CustomerId, PeriodViewModel Period, int Status, IReadOnlyCollection<OrderLineViewModel> Lines);
public sealed record PeriodViewModel(DateOnly StartDate, DateOnly EndDate);
public sealed record OrderLineViewModel(Guid Id, Guid ProductModelId, int Quantity);
public sealed record FaultViewModel(Guid Id, string Number, Guid CustomerId, string CustomerName,
    string ReporterName, string ReporterPhone, string Category, int Severity, string Description, int Status,
    DateTimeOffset OpenedAt);
public sealed record FaultPageViewModel(int Page, int PageSize, int TotalCount, int TotalPages,
    IReadOnlyCollection<FaultViewModel> Items);
public sealed class FaultFilterViewModel
{
    public string? Query { get; set; }
    public int? Status { get; set; }
    public int? Severity { get; set; }
    [DataType(DataType.Date)] public DateOnly? OpenedFrom { get; set; }
    [DataType(DataType.Date)] public DateOnly? OpenedTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
public sealed record FaultScreenViewModel(FaultPageViewModel Result, FaultFilterViewModel Filter);
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
    Guid? DefaultStorageLocationId, decimal TotalStock, bool IsLowStock);
public sealed record ComponentListPageViewModel(IReadOnlyCollection<ComponentCatalogViewModel> Components, string Query);
public sealed record SupplyNeedLineViewModel(Guid ComponentId, string ComponentName, string ComponentSku,
    string UnitOfMeasure, decimal Quantity, decimal? SuppliedQuantity);
public sealed record SupplyNeedListViewModel(Guid Id, int Status, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, IReadOnlyCollection<SupplyNeedLineViewModel> Lines);
public sealed class SupplyNeedLineInputViewModel
{
    [Required] public Guid ComponentId { get; set; }
    [Range(0.001, 999999)] public decimal Quantity { get; set; } = 1;
}
public sealed class SupplyNeedInputViewModel
{
    public Guid Id { get; set; }
    public List<SupplyNeedLineInputViewModel> Lines { get; set; } = [new()];
}
public sealed record SupplyNeedFormPageViewModel(SupplyNeedInputViewModel Form,
    IReadOnlyCollection<ComponentCatalogViewModel> Components, bool IsEdit);
public sealed record StorageLocationViewModel(Guid Id, string Code, string Warehouse, string Aisle,
    string Rack, string Shelf, bool IsDefaultForNewComponents);
public sealed class StorageLocationInputViewModel
{
    public Guid Id { get; set; }
    [Required, StringLength(80), Display(Name = "Raf kodu")] public string Code { get; set; } = string.Empty;
    [Required, StringLength(160), Display(Name = "Depo")] public string Warehouse { get; set; } = string.Empty;
    [Required, StringLength(40), Display(Name = "Koridor")] public string Aisle { get; set; } = string.Empty;
    [Required, StringLength(40), Display(Name = "Raf")] public string Rack { get; set; } = string.Empty;
    [Required, StringLength(40), Display(Name = "Göz")] public string Shelf { get; set; } = string.Empty;
    [Display(Name = "Yeni komponentlerde varsayılan raf")] public bool IsDefaultForNewComponents { get; set; }
}
public sealed class CompleteSupplyNeedLineViewModel
{
    public Guid ComponentId { get; set; }
    public bool Confirmed { get; set; }
    [Range(0.001, 999999)] public decimal SuppliedQuantity { get; set; }
}
public sealed class CompleteSupplyNeedViewModel
{
    public Guid Id { get; set; }
    public Guid StorageLocationId { get; set; }
    public List<CompleteSupplyNeedLineViewModel> Lines { get; set; } = [];
}
public sealed record SupplyNeedIndexPageViewModel(IReadOnlyCollection<SupplyNeedListViewModel> Lists,
    IReadOnlyCollection<StorageLocationViewModel> StorageLocations);
public sealed record BomLineViewModel(Guid ComponentId, string ComponentName, string ComponentSku, decimal Quantity, string UnitOfMeasure);
public sealed record BomViewModel(Guid Id, Guid ProductModelId, string ProductName, string ProductSku, int Version,
    IReadOnlyCollection<BomLineViewModel> Lines);

public class CreateComponentViewModel
{
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(80)] public string Sku { get; set; } = string.Empty;
    [Required, StringLength(40), Display(Name = "Ölçü birimi")] public string UnitOfMeasure { get; set; } = "adet";
    [Range(0, 999999), Display(Name = "Minimum stok")] public decimal MinimumStock { get; set; }
    [Url, Display(Name = "Görsel adresi")] public string? ImageUrl { get; set; }
    [Display(Name = "Varsayılan raf")] public Guid? DefaultStorageLocationId { get; set; }
    [Range(0, 999999), Display(Name = "Başlangıç stok adedi")] public decimal InitialStock { get; set; }
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
public sealed class EditComponentViewModel : CreateComponentViewModel { public Guid Id { get; set; } }
public sealed record ComponentFormPageViewModel(CreateComponentViewModel Form,
    IReadOnlyCollection<StorageLocationViewModel> StorageLocations, bool IsEdit);
public sealed class EditKitViewModel
{
    public Guid Id { get; set; }
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(80)] public string Sku { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    [Url, Display(Name = "Görsel adresi")] public string? ImageUrl { get; set; }
}
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
public sealed record PhysicalKitModelSummaryViewModel(Guid ProductModelId, string KitName, string KitSku,
    string? ImageUrl, int Total, int Available, int Faulty);
public sealed record PhysicalKitUnitPageViewModel(Guid ProductModelId, string KitName, string KitSku, string? ImageUrl,
    string Filter, int Page, int PageSize, int TotalCount, int TotalPages,
    IReadOnlyCollection<PhysicalKitListItemViewModel> Items);
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
public sealed class EditPhysicalKitViewModel
{
    public Guid Id { get; set; }
    public Guid ProductModelId { get; set; }
    [Required, StringLength(100), Display(Name = "Seri numarası")] public string SerialNumber { get; set; } = string.Empty;
    [Required, StringLength(200), Display(Name = "QR kod")] public string QrCode { get; set; } = string.Empty;
}
public sealed record PhysicalKitLabelViewModel(Guid Id, string KitName, string KitSku, string SerialNumber, string QrCode);
public sealed record PhysicalKitLabelsPageViewModel(DateTimeOffset CreatedAt,
    IReadOnlyCollection<PhysicalKitLabelViewModel> Labels, string? BackUrl = null);
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
public sealed class PhysicalKitSelectionViewModel
{
    public Guid ProductModelId { get; set; }
    public string Filter { get; set; } = "available";
    public List<Guid> ProductUnitIds { get; set; } = [];
}
public sealed class BulkRentPhysicalKitsViewModel
{
    public Guid ProductModelId { get; set; }
    public string KitName { get; set; } = string.Empty;
    [MinLength(1)] public List<Guid> ProductUnitIds { get; set; } = [];
    public List<string> SerialNumbers { get; set; } = [];
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
public sealed record BulkRentPhysicalKitsResultViewModel(Guid CustomerId, Guid OrderId, string OrderNumber,
    int KitCount, IReadOnlyCollection<BulkRentPhysicalKitItemViewModel> Kits);
public sealed record BulkRentPhysicalKitItemViewModel(Guid ProductUnitId, Guid AssignmentId, string SerialNumber,
    int Status);

public sealed record PortalAddressViewModel(Guid Id, string Title, string ContactName, string Phone, string Line1,
    string District, string City, string PostalCode);
public sealed record PortalProductModelViewModel(Guid Id, string Name, string Sku, string? Description, string? ImageUrl);
public sealed record PortalKitViewModel(Guid ProductUnitId, Guid AssignmentId, Guid OrderId, string OrderNumber,
    string KitName, string KitSku, string? ImageUrl, string SerialNumber, string QrCode, int UnitStatus, int AssignmentStatus,
    DateOnly StartDate, DateOnly EndDate, int OpenFaultCount);
public sealed record PortalKitLookupPageViewModel(string Identifier, bool HasSearched, string? Error);
public sealed record PortalKitDetailPageViewModel(PortalKitViewModel Kit,
    IReadOnlyCollection<PortalFaultViewModel> Faults);
public sealed record PortalOrderLineViewModel(Guid ProductModelId, string ProductName, string ProductSku, int Quantity);
public sealed record PortalOrderViewModel(Guid Id, string OrderNumber, Guid CustomerId, string CustomerName, int Status,
    DateOnly StartDate, DateOnly EndDate, DateTimeOffset CreatedAt, IReadOnlyCollection<PortalOrderLineViewModel> Lines,
    int AssignedKitCount = 0);
public sealed record OrderCustomerViewModel(Guid Id, string Name, string Email, bool IsActive,
    IReadOnlyCollection<PortalAddressViewModel> Addresses);
public sealed record CustomersPageViewModel(IReadOnlyCollection<OrderCustomerViewModel> Customers,
    IReadOnlyCollection<UserApiResponse> Accounts, string Query)
{
    public int ActiveCount => Customers.Count(item => item.IsActive);
    public int AddressCount => Customers.Sum(item => item.Addresses.Count);
}
public sealed class CustomerContactAccountViewModel
{
    [Required] public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    [Required, StringLength(100), Display(Name = "Ad")] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100), Display(Name = "Soyad")] public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(320), Display(Name = "Kullanıcı adı (e-posta)")] public string Username { get; set; } = string.Empty;
    [Required, MinLength(10), DataType(DataType.Password), Display(Name = "Şifre")] public string Password { get; set; } = string.Empty;
}
public sealed class CustomerInputViewModel
{
    public Guid Id { get; set; }
    [Required, StringLength(250), Display(Name = "Müşteri / kurum adı")] public string Name { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(320), Display(Name = "E-posta adresi")] public string Email { get; set; } = string.Empty;
    [Display(Name = "Aktif müşteri")] public bool IsActive { get; set; } = true;
}
public sealed class CustomerAddressInputViewModel
{
    public Guid CustomerId { get; set; }
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    [Required, StringLength(100), Display(Name = "Adres başlığı")] public string Title { get; set; } = string.Empty;
    [Required, StringLength(160), Display(Name = "İletişim kişisi")] public string ContactName { get; set; } = string.Empty;
    [Required, Phone, StringLength(40), Display(Name = "Telefon")] public string Phone { get; set; } = string.Empty;
    [Required, StringLength(500), Display(Name = "Açık adres")] public string Line1 { get; set; } = string.Empty;
    [Required, StringLength(120), Display(Name = "İlçe")] public string District { get; set; } = string.Empty;
    [Required, StringLength(120), Display(Name = "Şehir")] public string City { get; set; } = string.Empty;
    [StringLength(20), Display(Name = "Posta kodu")] public string PostalCode { get; set; } = string.Empty;
}
public sealed class CreateCustomerViewModel
{
    public CustomerInputViewModel Customer { get; set; } = new();
    public CustomerAddressInputViewModel Address { get; set; } = new() { Title = "Merkez" };
}
public sealed class AdminOrderInputViewModel
{
    [Required, Display(Name = "Müşteri")] public Guid CustomerId { get; set; }
    [Required, Display(Name = "Teslimat adresi")] public Guid AddressId { get; set; }
    [Required, DataType(DataType.Date), Display(Name = "Başlangıç tarihi")] public DateOnly StartDate { get; set; }
    [Required, DataType(DataType.Date), Display(Name = "Bitiş tarihi")] public DateOnly EndDate { get; set; }
    public List<PortalRentalLineInputViewModel> Lines { get; set; } = [new()];
}
public sealed record AdminOrderPageViewModel(AdminOrderInputViewModel Form,
    IReadOnlyCollection<OrderCustomerViewModel> Customers,
    IReadOnlyCollection<ProductModelCatalogViewModel> ProductModels);
public sealed record OrderKitViewModel(Guid ProductUnitId, Guid AssignmentId, Guid ProductModelId,
    string SerialNumber, int Status);
public sealed record OrderKitPreparationViewModel(Guid OrderId, int CreatedCount,
    IReadOnlyCollection<OrderKitViewModel> Kits);
public sealed record OrderDetailLineViewModel(Guid Id, Guid ProductModelId, string ProductName, string ProductSku,
    int Quantity, int CreatedKitCount);
public sealed record OrderDetailKitViewModel(Guid Id, Guid OrderLineId, Guid ProductModelId, string ProductName,
    string ProductSku, string SerialNumber, string QrCode, int Status);
public sealed record OrderDetailViewModel(Guid Id, string OrderNumber, string CustomerName, int Status,
    DateOnly StartDate, DateOnly EndDate, DateTimeOffset CreatedAt,
    IReadOnlyCollection<OrderDetailLineViewModel> Lines, IReadOnlyCollection<OrderDetailKitViewModel> Kits);
public sealed class PrepareOrderKitsViewModel
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public List<PortalRentalLineInputViewModel> Lines { get; set; } = [new()];
    public IReadOnlyCollection<ProductModelCatalogViewModel> ProductModels { get; set; } = [];
}
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
