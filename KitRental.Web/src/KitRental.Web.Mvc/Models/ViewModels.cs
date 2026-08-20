using KitRental.Web.Mvc.Branding;
using System.ComponentModel.DataAnnotations;

namespace KitRental.Web.Mvc.Models;

public sealed class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    public string? Error { get; set; }
    public BrandDefinition Brand { get; set; } = new();
}

public sealed record LoginApiResponse(string AccessToken, DateTimeOffset ExpiresAt, UserApiResponse User);
public sealed record UserApiResponse(Guid Id, string Email, string DisplayName, int Role, Guid? CustomerId, bool IsActive = true);
public sealed class CreateAdminUserViewModel
{
    [Required, StringLength(160), Display(Name = "Ad soyad")]
    public string DisplayName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(320), Display(Name = "Kullanıcı adı (e-posta)")]
    public string Email { get; set; } = string.Empty;
    [Required, MinLength(10), DataType(DataType.Password), Display(Name = "Şifre")]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password)), DataType(DataType.Password), Display(Name = "Şifre tekrarı")]
    public string PasswordConfirmation { get; set; } = string.Empty;
}
public sealed record AdminUsersPageViewModel(IReadOnlyCollection<UserApiResponse> Users);
public sealed record AuditEntryApiResponse(Guid Id, Guid ActorId, string EntityType, Guid EntityId,
    string Action, string? PreviousValue, string? NewValue, DateTimeOffset OccurredAt);
public sealed record AuditPageApiResponse(int Page, int PageSize, int TotalCount, int TotalPages,
    IReadOnlyCollection<AuditEntryApiResponse> Items);
public sealed class AuditFilterViewModel
{
    public string? Action { get; set; }
    public Guid? ActorId { get; set; }
    [DataType(DataType.Date)] public DateOnly? OccurredFrom { get; set; }
    [DataType(DataType.Date)] public DateOnly? OccurredTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
public sealed record AuditListItemViewModel(Guid Id, string ActorName, string ActorEmail, bool IsCustomer,
    string EntityType, Guid EntityId, string Action, string? PreviousValue, string? NewValue,
    DateTimeOffset OccurredAt);
public sealed record AuditScreenViewModel(int Page, int PageSize, int TotalCount, int TotalPages,
    IReadOnlyCollection<AuditListItemViewModel> Items, AuditFilterViewModel Filter,
    IReadOnlyCollection<UserApiResponse> Users);
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
    int OverdueOrders,
    int SoldKits,
    int CompletedPurchaseOrders,
    IReadOnlyCollection<DashboardReturnViewModel> ReturnsInProgress,
    IReadOnlyCollection<DashboardRentalExpiryViewModel> ExpiredRentalKits,
    IReadOnlyCollection<DashboardRentalExpiryViewModel> ExpiringRentalKits,
    IReadOnlyCollection<DashboardKitLocationViewModel> KitLocations);
public sealed record DashboardReturnViewModel(Guid Id, string CustomerName, int Status, string? Carrier,
    string? TrackingNumber, DateTimeOffset CreatedAt, int KitCount, string? RequesterName = null,
    string? RequesterPhone = null, string? ReturnAddress = null,
    double? Latitude = null, double? Longitude = null);
public sealed record DashboardRentalExpiryViewModel(Guid ProductUnitId, string KitName, string SerialNumber,
    string CustomerName, string OrderNumber, DateOnly EndDate, int DaysRemaining);
public sealed record DashboardKitLocationViewModel(Guid ProductUnitId, Guid ProductModelId, string KitName,
    string KitSku, string SerialNumber, string RecipientName, string AddressLine, string District, string City,
    int Status, double? Latitude = null, double? Longitude = null, string LocationCategory = "active");
public sealed record ProductUnitViewModel(Guid Id, Guid ProductModelId, string SerialNumber, string QrCode, int Status);
public sealed record InventoryItemViewModel(Guid Id, Guid ProductModelId, string ProductModelName,
    string ProductModelSku, string SerialNumber, string QrCode, int Status, DateTimeOffset CreatedAt,
    string? CustomerName = null, string? OrderNumber = null, DateOnly? RentalEndDate = null,
    int? DaysRemaining = null);
public sealed record InventoryPageViewModel(int Page, int PageSize, int TotalCount, int TotalPages,
    IReadOnlyCollection<InventoryItemViewModel> Items);
public sealed class InventoryFilterViewModel
{
    public string? Query { get; set; }
    public Guid? ProductModelId { get; set; }
    public int? Status { get; set; }
    [DataType(DataType.Date)] public DateOnly? CreatedFrom { get; set; }
    [DataType(DataType.Date)] public DateOnly? CreatedTo { get; set; }
    public string? RentalExpiry { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
public sealed record InventoryScreenViewModel(InventoryPageViewModel Result, InventoryFilterViewModel Filter,
    IReadOnlyCollection<ProductModelCatalogViewModel> ProductModels);
public sealed record OrderViewModel(Guid Id, string OrderNumber, Guid CustomerId, int Type,
    PeriodViewModel? Period, int Status, IReadOnlyCollection<OrderLineViewModel> Lines);
public sealed record PeriodViewModel(DateOnly StartDate, DateOnly EndDate);
public sealed record OrderLineViewModel(Guid Id, Guid ProductModelId, int Quantity);
public sealed record FaultViewModel(Guid Id, string Number, Guid CustomerId, string CustomerName,
    string ReporterName, string ReporterPhone, string ReporterAddress, string Category, int Severity, string Description, int Status,
    DateTimeOffset OpenedAt, int ApprovalStatus = 0, int Origin = 1);
public sealed record FaultPageViewModel(int Page, int PageSize, int TotalCount, int TotalPages,
    IReadOnlyCollection<FaultViewModel> Items);
public sealed record FaultGuideEntryViewModel(Guid Id, string Title, string Problem, string Solution,
    int DisplayOrder, bool IsActive, DateTimeOffset UpdatedAt, Guid? ProductModelId = null,
    string? ProductModelName = null);
public sealed class FaultGuideEntryInputViewModel
{
    public Guid? Id { get; set; }
    [Required, Display(Name = "Kit")] public Guid? ProductModelId { get; set; }
    [Required, StringLength(160), Display(Name = "Baslik")] public string Title { get; set; } = string.Empty;
    [Required, StringLength(2000), Display(Name = "Karsilasilan problem")] public string Problem { get; set; } = string.Empty;
    [Required, StringLength(4000), Display(Name = "Cozum onerisi")] public string Solution { get; set; } = string.Empty;
    [Range(0, 999), Display(Name = "Siralama")] public int DisplayOrder { get; set; }
    [Display(Name = "Aktif")] public bool IsActive { get; set; } = true;
}
public sealed record FaultGuidePageViewModel(IReadOnlyCollection<FaultGuideEntryViewModel> Entries,
    FaultGuideEntryInputViewModel Form,
    IReadOnlyCollection<ProductModelCatalogViewModel> ProductModels);
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
public sealed record PhysicalKitFaultHistoryViewModel(string Number, string Category, int Severity, int Status,
    string Description, DateTimeOffset OpenedAt, IReadOnlyCollection<string> StatusNotes);
public sealed record PhysicalKitDeliveryHistoryViewModel(Guid AssignmentId, string OrderNumber, int OrderStatus,
    int AssignmentStatus, string CustomerName, string CustomerEmail, DateOnly StartDate, DateOnly EndDate,
    DateTimeOffset CreatedAt, string RecipientName, string Phone, string AddressLine, string District,
    string City, DateTimeOffset? DeliveredAt, double? Latitude, double? Longitude);
public sealed record PhysicalKitReturnHistoryViewModel(Guid ReturnId, string ReturnNumber, int Status,
    string CustomerName, string RequesterName, string RequesterPhone, string ReturnAddress,
    DateTimeOffset CreatedAt, DateTimeOffset? ShippedAt, DateTimeOffset? ReceivedAt,
    double? Latitude, double? Longitude);
public sealed record PhysicalKitActivityViewModel(string Action, string Description, DateTimeOffset OccurredAt,
    string ActorDisplayName);
public sealed record PhysicalKitLocationViewModel(string RecipientName, string Phone, string AddressLine,
    string District, string City, DateTimeOffset? DeliveredAt, double? Latitude, double? Longitude);
public sealed record PhysicalKitDetailViewModel(PhysicalKitListItemViewModel Kit, PhysicalKitLocationViewModel? CurrentLocation,
    IReadOnlyCollection<PhysicalKitFaultHistoryViewModel> FaultHistory,
    IReadOnlyCollection<PhysicalKitDeliveryHistoryViewModel> DeliveryHistory,
    IReadOnlyCollection<PhysicalKitReturnHistoryViewModel> ReturnHistory,
    IReadOnlyCollection<PhysicalKitStatusEventViewModel> StatusHistory,
    IReadOnlyCollection<PhysicalKitActivityViewModel> ActivityHistory)
{
    public IReadOnlyCollection<PhysicalKitDeliveryHistoryViewModel> RentalHistory => DeliveryHistory;
}
public sealed record PhysicalKitLookupPageViewModel(string Identifier, bool HasSearched,
    PhysicalKitDetailViewModel? Result, string? Error);

public sealed class CreatePhysicalKitViewModel
{
    [Required, Display(Name = "Eğitim kiti")] public Guid ProductModelId { get; set; }
    [Range(1, int.MaxValue), Display(Name = "Oluşturulacak kit adedi")] public int Quantity { get; set; } = 1;
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
    [Required, TurkishPhone, Display(Name = "Telefon")] public string Phone { get; set; } = string.Empty;
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
    [Required, TurkishPhone, Display(Name = "Telefon")] public string Phone { get; set; } = string.Empty;
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
public sealed record PortalRentalCohortStudentViewModel(Guid Id, string FullName, string GuardianPhone,
    string AddressLine, int? CityId, int? DistrictId, string City, string District, Guid ProductModelId, string ProductModelName, string ProductModelSku, Guid? OrderId,
    Guid? AssignmentId, Guid? ProductUnitId, string? SerialNumber, string? QrCode, bool IsDeleted,
    bool HasActiveReturn, bool HasCompletedReturn = false, bool HasDeliveryForm = false, string? DeliveredTo = null,
    string? DeliveryPhone = null, string? DeliveryAddress = null, string? DeliveryDistrict = null,
    string? DeliveryCity = null, DateTimeOffset? DeliveredAt = null);
public sealed record PortalUnassignedCohortKitViewModel(Guid ProductUnitId, Guid AssignmentId, Guid OrderId,
    Guid ProductModelId, string ProductModelName, string ProductModelSku, string SerialNumber, string QrCode);
public sealed record PortalRentalCohortViewModel(Guid Id, Guid CustomerId, string Name, DateOnly StartDate,
    DateOnly EndDate, DateTimeOffset CreatedAt, Guid? OrderId, int StudentCount, int AssignedKitCount,
    IReadOnlyCollection<PortalRentalCohortStudentViewModel> Students,
    IReadOnlyCollection<PortalUnassignedCohortKitViewModel> UnassignedKits,
    string? OrderNumber = null, int? OrderStatus = null, bool IsApproved = false);
public sealed record PortalKitViewModel(Guid ProductUnitId, Guid AssignmentId, Guid OrderId, string OrderNumber,
    string KitName, string KitSku, string? ImageUrl, string SerialNumber, string QrCode, int UnitStatus, int AssignmentStatus,
    DateOnly StartDate, DateOnly EndDate, int OpenFaultCount, bool HasDeliveryForm,
    string? AssignedStudentName = null, string? AssignedStudentGuardianPhone = null,
    string? AssignedStudentAddressLine = null, string? AssignedStudentPeriodName = null, bool IsReturned = false,
    bool StudentOrderLocked = false);
public sealed record PortalKitLookupPageViewModel(string Identifier, bool HasSearched, string? Error);
public sealed record PortalKitDetailPageViewModel(PortalKitViewModel Kit,
    IReadOnlyCollection<PortalFaultViewModel> Faults);
public sealed record PortalFaultsPageViewModel(string CustomerName, string Query, int? Status, string State,
    int Page, int PageSize, int TotalCount, int TotalFaultCount, IReadOnlyCollection<PortalFaultViewModel> Faults)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public int FirstItem => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int LastItem => Math.Min(Page * PageSize, TotalCount);
}
public sealed record PortalKitsPageViewModel(string CustomerName, string Query, int? Status, bool? HasFault,
    bool? DeliveryFormMissing,
    int Page, int PageSize, int TotalCount, int TotalKitCount, IReadOnlyCollection<PortalKitViewModel> Kits)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public int FirstItem => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int LastItem => Math.Min(Page * PageSize, TotalCount);
}
public sealed record PortalOrderLineViewModel(Guid ProductModelId, string ProductName, string ProductSku, int Quantity);
public sealed record PortalOrderViewModel(Guid Id, string OrderNumber, Guid CustomerId, string CustomerName, int Type,
    int Status, DateOnly? StartDate, DateOnly? EndDate, DateTimeOffset CreatedAt, IReadOnlyCollection<PortalOrderLineViewModel> Lines,
    int AssignedKitCount = 0);
public sealed record OrderCustomerViewModel(Guid Id, string Name, string Email, bool IsActive,
    IReadOnlyCollection<PortalAddressViewModel> Addresses,
    IReadOnlyCollection<Guid>? AllowedProductModelIds = null);
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
    [Display(Name = "Kullanıma açılan kitler")] public List<string> AllowedProductModelSelection { get; set; } = ["all"];
    public IReadOnlyCollection<ProductModelCatalogViewModel> ProductModels { get; set; } = [];
    public bool AllowsAllProductModels => AllowedProductModelSelection.Count == 0 ||
        AllowedProductModelSelection.Any(item => string.Equals(item, "all", StringComparison.OrdinalIgnoreCase));
    public IReadOnlyCollection<Guid> SelectedAllowedProductModelIds => AllowsAllProductModels
        ? []
        : AllowedProductModelSelection
            .Select(item => Guid.TryParse(item, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
}
public sealed class CustomerAddressInputViewModel
{
    public Guid CustomerId { get; set; }
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    [Required, StringLength(100), Display(Name = "Adres başlığı")] public string Title { get; set; } = string.Empty;
    [Required, StringLength(160), Display(Name = "İletişim kişisi")] public string ContactName { get; set; } = string.Empty;
    [Required, TurkishPhone, StringLength(40), Display(Name = "Telefon")] public string Phone { get; set; } = string.Empty;
    [Required, StringLength(500), Display(Name = "Açık adres")] public string Line1 { get; set; } = string.Empty;
    [Required, StringLength(120), Display(Name = "İlçe")] public string District { get; set; } = string.Empty;
    [Required, StringLength(120), Display(Name = "Şehir")] public string City { get; set; } = string.Empty;
    [StringLength(20), Display(Name = "Posta kodu")] public string PostalCode { get; set; } = string.Empty;
}
public sealed class CreateCustomerViewModel
{
    public CustomerInputViewModel Customer { get; set; } = new();
    public CustomerAddressInputViewModel Address { get; set; } = new() { Title = "Merkez" };
    [Display(Name = "Kullanıma açılan kitler")] public List<string> AllowedProductModelSelection { get; set; } = ["all"];
    public IReadOnlyCollection<ProductModelCatalogViewModel> ProductModels { get; set; } = [];
    public bool AllowsAllProductModels => AllowedProductModelSelection.Count == 0 ||
        AllowedProductModelSelection.Any(item => string.Equals(item, "all", StringComparison.OrdinalIgnoreCase));
    public IReadOnlyCollection<Guid> SelectedAllowedProductModelIds => AllowsAllProductModels
        ? []
        : AllowedProductModelSelection
            .Select(item => Guid.TryParse(item, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
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
public sealed class PurchaseOrderInputViewModel
{
    [Required, Display(Name = "Müşteri")] public Guid CustomerId { get; set; }
    [Required, Display(Name = "Teslimat adresi")] public Guid AddressId { get; set; }
    public List<PortalRentalLineInputViewModel> Lines { get; set; } = [new()];
}
public sealed record PurchaseOrderPageViewModel(PurchaseOrderInputViewModel Form,
    IReadOnlyCollection<OrderCustomerViewModel> Customers,
    IReadOnlyCollection<ProductModelCatalogViewModel> ProductModels);
public sealed record OrderKitViewModel(Guid ProductUnitId, Guid AssignmentId, Guid ProductModelId,
    string SerialNumber, int Status);
public sealed record OrderKitPreparationViewModel(Guid OrderId, int CreatedCount, int ReusedCount,
    IReadOnlyCollection<OrderKitViewModel> Kits);
public sealed record OrderDetailLineViewModel(Guid Id, Guid ProductModelId, string ProductName, string ProductSku,
    int Quantity, int CreatedKitCount);
public sealed record OrderDetailKitViewModel(Guid Id, Guid OrderLineId, Guid ProductModelId, string ProductName,
    string ProductSku, string SerialNumber, string QrCode, int Status);
public sealed record OrderDetailViewModel(Guid Id, string OrderNumber, Guid CustomerId, string CustomerName,
    int Type, int Status, DateOnly? StartDate, DateOnly? EndDate, DateTimeOffset CreatedAt, Guid? RentalCohortId,
    IReadOnlyCollection<OrderDetailLineViewModel> Lines, IReadOnlyCollection<OrderDetailKitViewModel> Kits);
public sealed class PrepareOrderKitsViewModel
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public bool UseAvailableKits { get; set; }
    public Guid? RentalCohortId { get; set; }
    public List<PortalRentalLineInputViewModel> Lines { get; set; } = [new()];
    public IReadOnlyCollection<ProductModelCatalogViewModel> ProductModels { get; set; } = [];
    public IReadOnlyCollection<PortalRentalCohortViewModel> RentalCohorts { get; set; } = [];
}
public sealed record PortalFaultStatusViewModel(int Previous, int Current, DateTimeOffset OccurredAt, string Note);
public sealed record PortalShipmentEventViewModel(int Status, DateTimeOffset OccurredAt, string Location, string Description);
public sealed record PortalShipmentViewModel(int Type, string Carrier, string TrackingNumber, int Status,
    IReadOnlyCollection<PortalShipmentEventViewModel> Events);
public sealed record PortalFaultViewModel(Guid Id, string Number, Guid ProductUnitId, string KitName, string SerialNumber,
    string Category, int Severity, string Description, int Status, DateTimeOffset OpenedAt,
    IReadOnlyCollection<PortalFaultStatusViewModel> History, IReadOnlyCollection<PortalShipmentViewModel> Shipments,
    string ReporterName = "", string ReporterPhone = "", string ReporterAddress = "", int ApprovalStatus = 0,
    int Origin = 1);
public sealed record PublicFaultKitViewModel(string QrCode, Guid ProductUnitId, string KitName, string SerialNumber);
public sealed record PublicKitActionViewModel(string QrCode, string KitName, string SerialNumber);
public sealed record PublicFaultTroubleshootingViewModel(string QrCode, string KitName, string SerialNumber,
    IReadOnlyCollection<FaultGuideEntryViewModel> Entries);

public sealed record EmailDeliveryViewModel(Guid Id, string Recipient, string RecipientName, string Subject,
    string Body, int Status, DateTimeOffset OccurredAt, string? Error);
public sealed class PublicFaultFormViewModel
{
    public Guid? FaultId { get; set; }
    [Required] public string QrCode { get; set; } = string.Empty;
    public string KitName { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    [Required, StringLength(160), Display(Name = "Ad soyad")] public string ReporterName { get; set; } = string.Empty;
    [Required, TurkishPhone, StringLength(40), Display(Name = "Telefon numarası")] public string ReporterPhone { get; set; } = string.Empty;
    [Required, StringLength(120), Display(Name = "İl")] public string City { get; set; } = string.Empty;
    [Required, StringLength(120), Display(Name = "İlçe")] public string District { get; set; } = string.Empty;
    [Required, StringLength(1000), Display(Name = "Adres")] public string ReporterAddress { get; set; } = string.Empty;
    [Display(Name = "Enlem")] public double? Latitude { get; set; }
    [Display(Name = "Boylam")] public double? Longitude { get; set; }
    [Required, StringLength(4000, MinimumLength = 10), Display(Name = "Ariza nedeni")]
    public string Description { get; set; } = string.Empty;
}
public sealed record PublicFaultContextViewModel(Guid? FaultId, string? ReporterName, string? ReporterPhone,
    string? ReporterAddress, string? District, string? City, string? Category, string? Description,
    double? Latitude, double? Longitude);
public sealed class PublicReturnFormViewModel
{
    [Required] public string QrCode { get; set; } = string.Empty;
    public string KitName { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    [Required, Display(Name = "İade nedeni")] public int? ReturnReason { get; set; }
    [Required, StringLength(160), Display(Name = "Ad soyad")] public string RequesterName { get; set; } = string.Empty;
    [Required, TurkishPhone, StringLength(40), Display(Name = "Telefon numarası")] public string RequesterPhone { get; set; } = string.Empty;
    [Required, StringLength(120), Display(Name = "İl")] public string City { get; set; } = string.Empty;
    [Required, StringLength(120), Display(Name = "İlçe")] public string District { get; set; } = string.Empty;
    [Required, StringLength(1000), Display(Name = "Adres")] public string ReturnAddress { get; set; } = string.Empty;
    [Display(Name = "Enlem")] public double? Latitude { get; set; }
    [Display(Name = "Boylam")] public double? Longitude { get; set; }
}
public sealed record PublicKitDeliveryContextViewModel(string? RecipientName, string? RecipientPhone,
    string? AddressLine, string? District, string? City, double? Latitude, double? Longitude);
public sealed class PublicDeliveryFormViewModel
{
    [Required] public string QrCode { get; set; } = string.Empty;
    public string KitName { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    [Required, StringLength(160), Display(Name = "Ad soyad")] public string RecipientName { get; set; } = string.Empty;
    [Required, TurkishPhone, StringLength(40), Display(Name = "Telefon numarası")] public string RecipientPhone { get; set; } = string.Empty;
    [Required, StringLength(120), Display(Name = "İl")] public string City { get; set; } = string.Empty;
    [Required, StringLength(120), Display(Name = "İlçe")] public string District { get; set; } = string.Empty;
    [Required, StringLength(1000), Display(Name = "Adres")] public string AddressLine { get; set; } = string.Empty;
    [Display(Name = "Enlem")] public double? Latitude { get; set; }
    [Display(Name = "Boylam")] public double? Longitude { get; set; }
}
public sealed record CustomerPortalViewModel(string CustomerName, string CustomerEmail, int TotalRentedKitCount,
    int UndeliveredKitCount, int ActiveKitCount, int UnassignedKitCount, int PendingRequestCount, int OpenFaultCount,
    int CompletedFaultCount, int ExpiredRentalKitCount, int ReturnProcessStartedKitCount, int ReturnedKitCount,
    IReadOnlyCollection<PortalKitViewModel> Kits,
    IReadOnlyCollection<PortalOrderViewModel> Orders, IReadOnlyCollection<PortalFaultViewModel> Faults,
    IReadOnlyCollection<PortalAddressViewModel> Addresses, IReadOnlyCollection<PortalProductModelViewModel> ProductModels,
    IReadOnlyCollection<PortalKitReturnViewModel> Returns,
    IReadOnlyCollection<DashboardKitLocationViewModel> KitLocations,
    IReadOnlyCollection<PortalRentalCohortViewModel> RentalCohorts);
public sealed record PortalReturnListItemViewModel(Guid ProductUnitId, Guid AssignmentId, Guid? ReturnId,
    string KitName, string KitSku, string SerialNumber, string OrderNumber, DateOnly StartDate, DateOnly EndDate,
    int UnitStatus, int AssignmentStatus, int ReturnStatus, int OpenFaultCount, string ReturnStateKey,
    string ReturnState, bool StudentOrderLocked = false);
public sealed record PortalReturnsPageViewModel(string CustomerName, string Query, string State, int Page,
    int PageSize, int TotalCount, int TotalKitCount, int TotalPages, int FirstItem, int LastItem,
    IReadOnlyCollection<PortalReturnListItemViewModel> Returns);
public sealed record PortalKitReturnItemViewModel(Guid AssignmentId, Guid ProductUnitId, Guid OrderId,
    string KitName, string SerialNumber);
public sealed record PortalKitReturnViewModel(Guid Id, Guid CustomerId, string CustomerName, int Status,
    string? Carrier, string? TrackingNumber, DateTimeOffset CreatedAt, DateTimeOffset? ShippedAt,
    string? RequesterName, string? RequesterPhone, string? ReturnAddress,
    double? Latitude, double? Longitude,
    IReadOnlyCollection<PortalKitReturnItemViewModel> Items);
public sealed class PortalRentalLineInputViewModel
{
    [Required, Display(Name = "Eğitim kiti")] public Guid ProductModelId { get; set; }
    [Range(1, int.MaxValue), Display(Name = "Adet")] public int Quantity { get; set; } = 1;
}
public sealed class PortalFaultRequestViewModel
{
    [Required] public Guid AssignmentId { get; set; }
    public string KitName { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    [Required, StringLength(160), Display(Name = "Ad soyad")] public string ReporterName { get; set; } = string.Empty;
    [Required, TurkishPhone, StringLength(40), Display(Name = "Telefon numarası")] public string ReporterPhone { get; set; } = string.Empty;
    [Required, StringLength(120), Display(Name = "İl")] public string City { get; set; } = string.Empty;
    [Required, StringLength(120), Display(Name = "İlçe")] public string District { get; set; } = string.Empty;
    [Required, StringLength(1000), Display(Name = "Adres")] public string ReporterAddress { get; set; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 10), Display(Name = "Arıza nedeni")] public string Description { get; set; } = string.Empty;
}
public sealed record PortalFaultRequestPageViewModel(PortalFaultRequestViewModel Form,
    IReadOnlyCollection<PortalKitViewModel> ActiveKits);

public sealed class PortalStudentReturnFormViewModel
{
    [Required] public Guid CohortId { get; set; }
    [Required] public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string KitName { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    [Required, Display(Name = "İade nedeni")] public int? ReturnReason { get; set; }
    [Required, StringLength(160), Display(Name = "Ad soyad")] public string RequesterName { get; set; } = string.Empty;
    [Required, TurkishPhone, StringLength(40), Display(Name = "Telefon numarası")] public string RequesterPhone { get; set; } = string.Empty;
    [Required, StringLength(120), Display(Name = "İl")] public string City { get; set; } = string.Empty;
    [Required, StringLength(120), Display(Name = "İlçe")] public string District { get; set; } = string.Empty;
    [Required, StringLength(1000), Display(Name = "Adres")] public string ReturnAddress { get; set; } = string.Empty;
}

public sealed class RentalCohortInputViewModel
{
    public Guid? Id { get; set; }
    [Required, StringLength(200), Display(Name = "Dönem adı")] public string Name { get; set; } = string.Empty;
    [Required, DataType(DataType.Date), Display(Name = "Başlangıç tarihi")] public DateOnly StartDate { get; set; }
    [Required, DataType(DataType.Date), Display(Name = "Bitiş tarihi")] public DateOnly EndDate { get; set; }
}

public sealed class RentalCohortStudentInputViewModel
{
    public Guid? Id { get; set; }
    public Guid CohortId { get; set; }
    [Required, StringLength(160), Display(Name = "Öğrenci adı soyadı")] public string FullName { get; set; } = string.Empty;
    [Required, TurkishPhone, StringLength(40), Display(Name = "Veli telefon numarası")] public string GuardianPhone { get; set; } = string.Empty;
    [Required, StringLength(1000), Display(Name = "Adres bilgileri")] public string AddressLine { get; set; } = string.Empty;
    [Required, Display(Name = "İl")] public int CityId { get; set; }
    [Required, Display(Name = "İlçe")] public int DistrictId { get; set; }
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    [Required, Display(Name = "Eğitim kiti")] public Guid ProductModelId { get; set; }
}

public sealed record RentalCohortsPageViewModel(string CustomerName,
    IReadOnlyCollection<PortalRentalCohortViewModel> Cohorts, RentalCohortInputViewModel Form,
    IReadOnlyCollection<string> PeriodNameOptions, string? PeriodName, string? ApprovalStatus,
    int Page, int PageSize, int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public int FirstItem => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int LastItem => Math.Min(Page * PageSize, TotalCount);
}
public sealed record RentalCohortDetailPageViewModel(PortalRentalCohortViewModel Cohort,
    RentalCohortStudentInputViewModel StudentForm, IReadOnlyCollection<PortalProductModelViewModel> ProductModels,
    IReadOnlyCollection<PortalRentalCohortStudentViewModel> Students, string? StudentQuery,
    Guid? ProductModelId, string? AssignmentState, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public int FirstItem => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int LastItem => Math.Min(Page * PageSize, TotalCount);
}

public sealed class RentalCohortStudentImportPreviewRowViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string GuardianPhone { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public int CityId { get; set; }
    public int DistrictId { get; set; }
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public Guid ProductModelId { get; set; }
}

public sealed class RentalCohortStudentImportPreviewViewModel
{
    public Guid CohortId { get; set; }
    public string CohortName { get; set; } = string.Empty;
    public List<RentalCohortStudentImportPreviewRowViewModel> Rows { get; set; } = [];
    public IReadOnlyCollection<PortalProductModelViewModel> ProductModels { get; set; } = [];
}

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




