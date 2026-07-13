using System.Globalization;
using KitRental.Core.Domain.Auditing;
using KitRental.Core.Domain.Customers;
using KitRental.Core.Domain.Inventory;
using KitRental.Core.Domain.Logistics;
using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Rentals;
using KitRental.Core.Domain.Returns;
using KitRental.Core.Domain.Support;
using KitRental.Core.Domain.Manufacturing;
using KitRental.Core.Domain.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KitRental.Core.Infrastructure.Persistence;

public sealed class KitRentalDbContext(DbContextOptions<KitRentalDbContext> options) : DbContext(options)
{
    public DbSet<ProductModel> ProductModels => Set<ProductModel>();
    public DbSet<ProductUnit> ProductUnits => Set<ProductUnit>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<RentalOrder> RentalOrders => Set<RentalOrder>();
    public DbSet<RentalAssignment> RentalAssignments => Set<RentalAssignment>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<FaultTicket> FaultTickets => Set<FaultTicket>();
    public DbSet<ReturnInspection> ReturnInspections => Set<ReturnInspection>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<Component> Components => Set<Component>();
    public DbSet<StorageLocation> StorageLocations => Set<StorageLocation>();
    public DbSet<ComponentStock> ComponentStocks => Set<ComponentStock>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<BillOfMaterials> BillsOfMaterials => Set<BillOfMaterials>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureProductModel(modelBuilder.Entity<ProductModel>());
        ConfigureProductUnit(modelBuilder.Entity<ProductUnit>());
        ConfigureCustomer(modelBuilder.Entity<Customer>());
        ConfigureOrder(modelBuilder.Entity<RentalOrder>());
        ConfigureAssignment(modelBuilder.Entity<RentalAssignment>());
        ConfigureShipment(modelBuilder.Entity<Shipment>());
        ConfigureFaultTicket(modelBuilder.Entity<FaultTicket>());
        ConfigureInspection(modelBuilder.Entity<ReturnInspection>());
        ConfigureAudit(modelBuilder.Entity<AuditEntry>());
        ConfigureComponent(modelBuilder.Entity<Component>());
        ConfigureStorageLocation(modelBuilder.Entity<StorageLocation>());
        ConfigureComponentStock(modelBuilder.Entity<ComponentStock>());
        ConfigureStockMovement(modelBuilder.Entity<StockMovement>());
        ConfigureBillOfMaterials(modelBuilder.Entity<BillOfMaterials>());
    }

    private static void ConfigureProductModel(EntityTypeBuilder<ProductModel> builder)
    {
        builder.ToTable("ProductModels");
        builder.HasKey(model => model.Id);
        builder.Property(model => model.Name).HasMaxLength(200).IsRequired();
        builder.Property(model => model.Sku).HasMaxLength(80).IsRequired();
        builder.Property(model => model.Description).HasMaxLength(2000);
        builder.Property(model => model.ImageUrl).HasMaxLength(1000);
        builder.HasIndex(model => model.Sku).IsUnique();
        AddRowVersion(builder);
    }

    private static void ConfigureProductUnit(EntityTypeBuilder<ProductUnit> builder)
    {
        builder.ToTable("ProductUnits");
        builder.HasKey(unit => unit.Id);
        builder.Property(unit => unit.SerialNumber).HasMaxLength(120).IsRequired();
        builder.Property(unit => unit.QrCode).HasMaxLength(200).IsRequired();
        builder.HasIndex(unit => unit.SerialNumber).IsUnique();
        builder.HasIndex(unit => unit.QrCode).IsUnique();
        builder.HasOne<ProductModel>().WithMany().HasForeignKey(unit => unit.ProductModelId).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsMany(unit => unit.History, events =>
        {
            events.ToTable("InventoryEvents");
            events.WithOwner().HasForeignKey("OwnerProductUnitId");
            events.HasKey(item => item.Id);
            events.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            events.HasIndex(item => item.ProductUnitId);
        });
        builder.Navigation(unit => unit.History).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);
        AddRowVersion(builder);
    }

    private static void ConfigureCustomer(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(customer => customer.Id);
        builder.Property(customer => customer.Name).HasMaxLength(250).IsRequired();
        builder.Property(customer => customer.Email).HasMaxLength(320).IsRequired();
        builder.HasIndex(customer => customer.Email).IsUnique();
        builder.OwnsMany(customer => customer.Addresses, addresses =>
        {
            addresses.ToTable("CustomerAddresses");
            addresses.WithOwner().HasForeignKey("CustomerId");
            addresses.HasKey(address => address.Id);
            addresses.Property(address => address.Title).HasMaxLength(100).IsRequired();
            addresses.Property(address => address.ContactName).HasMaxLength(160).IsRequired();
            addresses.Property(address => address.Phone).HasMaxLength(40).IsRequired();
            addresses.Property(address => address.Line1).HasMaxLength(500).IsRequired();
            addresses.Property(address => address.District).HasMaxLength(120).IsRequired();
            addresses.Property(address => address.City).HasMaxLength(120).IsRequired();
            addresses.Property(address => address.PostalCode).HasMaxLength(20);
        });
        builder.Navigation(customer => customer.Addresses).HasField("_addresses").UsePropertyAccessMode(PropertyAccessMode.Field);
        AddRowVersion(builder);
    }

    private static void ConfigureOrder(EntityTypeBuilder<RentalOrder> builder)
    {
        builder.ToTable("RentalOrders");
        builder.HasKey(order => order.Id);
        builder.Property(order => order.OrderNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(order => order.OrderNumber).IsUnique();
        builder.HasIndex(order => new { order.CustomerId, order.Status });
        builder.Property(order => order.Period).HasConversion(RentalPeriodConverter()).HasMaxLength(21);
        builder.HasOne<Customer>().WithMany().HasForeignKey(order => order.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(order => order.DeliveryAddress, address =>
        {
            address.Property(item => item.ContactName).HasColumnName("DeliveryContactName").HasMaxLength(160);
            address.Property(item => item.Phone).HasColumnName("DeliveryPhone").HasMaxLength(40);
            address.Property(item => item.Line1).HasColumnName("DeliveryLine1").HasMaxLength(500);
            address.Property(item => item.District).HasColumnName("DeliveryDistrict").HasMaxLength(120);
            address.Property(item => item.City).HasColumnName("DeliveryCity").HasMaxLength(120);
            address.Property(item => item.PostalCode).HasColumnName("DeliveryPostalCode").HasMaxLength(20);
        });
        builder.OwnsMany(order => order.Lines, lines =>
        {
            lines.ToTable("RentalOrderLines");
            lines.WithOwner().HasForeignKey("RentalOrderId");
            lines.HasKey(line => line.Id);
            lines.HasOne<ProductModel>().WithMany().HasForeignKey(line => line.ProductModelId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Navigation(order => order.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.OwnsMany(order => order.History, history =>
        {
            history.ToTable("OrderStatusEvents");
            history.WithOwner().HasForeignKey("RentalOrderId");
            history.HasKey(item => item.Id);
            history.Property(item => item.Reason).HasMaxLength(500).IsRequired();
        });
        builder.Navigation(order => order.History).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);
        AddRowVersion(builder);
    }

    private static void ConfigureAssignment(EntityTypeBuilder<RentalAssignment> builder)
    {
        builder.ToTable("RentalAssignments");
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Period).HasConversion(RentalPeriodConverter()).HasMaxLength(21);
        builder.HasIndex(assignment => new { assignment.ProductUnitId, assignment.Status });
        builder.HasOne<ProductUnit>().WithMany().HasForeignKey(assignment => assignment.ProductUnitId).OnDelete(DeleteBehavior.Restrict);
        AddRowVersion(builder);
    }

    private static void ConfigureShipment(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("Shipments");
        builder.HasKey(shipment => shipment.Id);
        builder.Property(shipment => shipment.Carrier).HasMaxLength(120).IsRequired();
        builder.Property(shipment => shipment.TrackingNumber).HasMaxLength(160).IsRequired();
        builder.HasIndex(shipment => shipment.TrackingNumber).IsUnique();
        builder.HasIndex(shipment => shipment.OrderId);
        builder.OwnsMany(shipment => shipment.Events, events =>
        {
            events.ToTable("ShipmentEvents");
            events.WithOwner().HasForeignKey("ShipmentId");
            events.HasKey(item => item.Id);
            events.Property(item => item.Location).HasMaxLength(200);
            events.Property(item => item.Description).HasMaxLength(1000).IsRequired();
        });
        builder.Navigation(shipment => shipment.Events).HasField("_events").UsePropertyAccessMode(PropertyAccessMode.Field);
        AddRowVersion(builder);
    }

    private static void ConfigureFaultTicket(EntityTypeBuilder<FaultTicket> builder)
    {
        builder.ToTable("FaultTickets");
        builder.HasKey(ticket => ticket.Id);
        builder.Property(ticket => ticket.Number).HasMaxLength(50).IsRequired();
        builder.Property(ticket => ticket.Category).HasMaxLength(160).IsRequired();
        builder.Property(ticket => ticket.Description).HasMaxLength(4000).IsRequired();
        builder.HasIndex(ticket => ticket.Number).IsUnique();
        builder.HasIndex(ticket => new { ticket.CustomerId, ticket.Status });
        builder.OwnsMany(ticket => ticket.History, history =>
        {
            history.ToTable("FaultStatusEvents");
            history.WithOwner().HasForeignKey("FaultTicketId");
            history.HasKey(item => item.Id);
            history.Property(item => item.Note).HasMaxLength(2000).IsRequired();
        });
        builder.Navigation(ticket => ticket.History).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);
        AddRowVersion(builder);
    }

    private static void ConfigureInspection(EntityTypeBuilder<ReturnInspection> builder)
    {
        builder.ToTable("ReturnInspections");
        builder.HasKey(inspection => inspection.Id);
        builder.Property(inspection => inspection.DamageCharge).HasPrecision(18, 2);
        builder.HasIndex(inspection => new { inspection.OrderId, inspection.ProductUnitId });
        builder.OwnsMany(inspection => inspection.Items, items =>
        {
            items.ToTable("InspectionItems");
            items.WithOwner().HasForeignKey("ReturnInspectionId");
            items.HasKey(item => item.Id);
            items.Property(item => item.Name).HasMaxLength(200).IsRequired();
            items.Property(item => item.Note).HasMaxLength(1000);
        });
        builder.Navigation(inspection => inspection.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
        AddRowVersion(builder);
    }

    private static void ConfigureAudit(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.EntityType).HasMaxLength(160).IsRequired();
        builder.Property(entry => entry.Action).HasMaxLength(160).IsRequired();
        builder.Property(entry => entry.PreviousValue).HasMaxLength(4000);
        builder.Property(entry => entry.NewValue).HasMaxLength(4000);
        builder.HasIndex(entry => new { entry.EntityType, entry.EntityId, entry.OccurredAt });
    }

    private static void ConfigureComponent(EntityTypeBuilder<Component> builder)
    {
        builder.ToTable("Components");
        builder.HasKey(component => component.Id);
        builder.Property(component => component.Name).HasMaxLength(200).IsRequired();
        builder.Property(component => component.Sku).HasMaxLength(80).IsRequired();
        builder.Property(component => component.UnitOfMeasure).HasMaxLength(40).IsRequired();
        builder.Property(component => component.ImageUrl).HasMaxLength(1000);
        builder.Property(component => component.MinimumStock).HasPrecision(18, 3);
        builder.HasIndex(component => component.Sku).IsUnique();
        AddRowVersion(builder);
    }

    private static void ConfigureStorageLocation(EntityTypeBuilder<StorageLocation> builder)
    {
        builder.ToTable("StorageLocations");
        builder.HasKey(location => location.Id);
        builder.Property(location => location.Code).HasMaxLength(80).IsRequired();
        builder.Property(location => location.Warehouse).HasMaxLength(160).IsRequired();
        builder.Property(location => location.Aisle).HasMaxLength(40).IsRequired();
        builder.Property(location => location.Rack).HasMaxLength(40).IsRequired();
        builder.Property(location => location.Shelf).HasMaxLength(40).IsRequired();
        builder.HasIndex(location => location.Code).IsUnique();
        AddRowVersion(builder);
    }

    private static void ConfigureComponentStock(EntityTypeBuilder<ComponentStock> builder)
    {
        builder.ToTable("ComponentStocks");
        builder.HasKey(stock => stock.Id);
        builder.Property(stock => stock.Quantity).HasPrecision(18, 3);
        builder.HasIndex(stock => new { stock.ComponentId, stock.StorageLocationId }).IsUnique();
        builder.HasOne<Component>().WithMany().HasForeignKey(stock => stock.ComponentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StorageLocation>().WithMany().HasForeignKey(stock => stock.StorageLocationId).OnDelete(DeleteBehavior.Restrict);
        AddRowVersion(builder);
    }

    private static void ConfigureStockMovement(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");
        builder.HasKey(movement => movement.Id);
        builder.Property(movement => movement.Quantity).HasPrecision(18, 3);
        builder.Property(movement => movement.Reference).HasMaxLength(500).IsRequired();
        builder.Ignore(movement => movement.SignedQuantity);
        builder.HasIndex(movement => new { movement.ComponentId, movement.OccurredAt });
        builder.HasIndex(movement => movement.TransferId);
        builder.HasOne<Component>().WithMany().HasForeignKey(movement => movement.ComponentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StorageLocation>().WithMany().HasForeignKey(movement => movement.StorageLocationId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureBillOfMaterials(EntityTypeBuilder<BillOfMaterials> builder)
    {
        builder.ToTable("BillsOfMaterials");
        builder.HasKey(bom => bom.Id);
        builder.HasIndex(bom => new { bom.ProductModelId, bom.Version }).IsUnique();
        builder.HasOne<ProductModel>().WithMany().HasForeignKey(bom => bom.ProductModelId).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsMany(bom => bom.Lines, lines =>
        {
            lines.ToTable("BillOfMaterialsLines");
            lines.WithOwner().HasForeignKey("BillOfMaterialsId");
            lines.HasKey(line => line.Id);
            lines.Property(line => line.Quantity).HasPrecision(18, 3);
            lines.HasOne<Component>().WithMany().HasForeignKey(line => line.ComponentId).OnDelete(DeleteBehavior.Restrict);
            lines.HasIndex(line => line.ComponentId);
        });
        builder.Navigation(bom => bom.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        AddRowVersion(builder);
    }

    private static ValueConverter<RentalPeriod, string> RentalPeriodConverter() =>
        new(period => SerializePeriod(period), value => DeserializePeriod(value));

    private static string SerializePeriod(RentalPeriod period) =>
        $"{period.StartDate:yyyy-MM-dd}|{period.EndDate:yyyy-MM-dd}";

    private static RentalPeriod DeserializePeriod(string value) =>
        new(
            DateOnly.ParseExact(value[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateOnly.ParseExact(value[11..], "yyyy-MM-dd", CultureInfo.InvariantCulture));

    private static void AddRowVersion<TEntity>(EntityTypeBuilder<TEntity> builder) where TEntity : class =>
        builder.Property<byte[]>("RowVersion").IsRowVersion();
}
