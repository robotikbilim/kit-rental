using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    PreviousValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Recipient = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailDeliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FaultGuideEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Problem = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Solution = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaultGuideEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FaultTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ReporterName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ReporterPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ReporterAddress = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaultTickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KitReturnRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Carrier = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    TrackingNumber = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ShippedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequesterName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    RequesterPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ReturnAddress = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitReturnRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductModels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReturnInspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DamageCharge = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnInspections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FaultTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Carrier = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TrackingNumber = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorageLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Warehouse = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Aisle = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Rack = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Shelf = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDefaultForNewComponents = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageLocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplyNeedLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplyNeedLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAddresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Line1 = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    District = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    City = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerAddresses_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RentalOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Period = table.Column<string>(type: "nvarchar(21)", maxLength: 21, nullable: true),
                    DeliveryContactName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    DeliveryPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DeliveryLine1 = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DeliveryDistrict = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DeliveryCity = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DeliveryPostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RentalOrders_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FaultStatusEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Previous = table.Column<int>(type: "int", nullable: false),
                    Current = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FaultTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaultStatusEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaultStatusEvents_FaultTickets_FaultTicketId",
                        column: x => x.FaultTicketId,
                        principalTable: "FaultTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KitReturnItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KitReturnRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitReturnItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KitReturnItems_KitReturnRequests_KitReturnRequestId",
                        column: x => x.KitReturnRequestId,
                        principalTable: "KitReturnRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BillsOfMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillsOfMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillsOfMaterials_ProductModels_ProductModelId",
                        column: x => x.ProductModelId,
                        principalTable: "ProductModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    QrCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductUnits_ProductModels_ProductModelId",
                        column: x => x.ProductModelId,
                        principalTable: "ProductModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsPresent = table.Column<bool>(type: "bit", nullable: false),
                    IsDamaged = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReturnInspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionItems_ReturnInspections_ReturnInspectionId",
                        column: x => x.ReturnInspectionId,
                        principalTable: "ReturnInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentEvents_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Components",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MinimumStock = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DefaultStorageLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Components", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Components_StorageLocations_DefaultStorageLocationId",
                        column: x => x.DefaultStorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OrderStatusEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Previous = table.Column<int>(type: "int", nullable: false),
                    Current = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RentalOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatusEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderStatusEvents_RentalOrders_RentalOrderId",
                        column: x => x.RentalOrderId,
                        principalTable: "RentalOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RentalOrderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    RentalOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RentalOrderLines_ProductModels_ProductModelId",
                        column: x => x.ProductModelId,
                        principalTable: "ProductModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RentalOrderLines_RentalOrders_RentalOrderId",
                        column: x => x.RentalOrderId,
                        principalTable: "RentalOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousStatus = table.Column<int>(type: "int", nullable: true),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OwnerProductUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryEvents_ProductUnits_OwnerProductUnitId",
                        column: x => x.OwnerProductUnitId,
                        principalTable: "ProductUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderProductUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RentalOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderProductUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderProductUnits_ProductUnits_ProductUnitId",
                        column: x => x.ProductUnitId,
                        principalTable: "ProductUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderProductUnits_RentalOrders_RentalOrderId",
                        column: x => x.RentalOrderId,
                        principalTable: "RentalOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RentalAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Period = table.Column<string>(type: "nvarchar(21)", maxLength: 21, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RentalAssignments_ProductUnits_ProductUnitId",
                        column: x => x.ProductUnitId,
                        principalTable: "ProductUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BillOfMaterialsLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComponentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    BillOfMaterialsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillOfMaterialsLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillOfMaterialsLines_BillsOfMaterials_BillOfMaterialsId",
                        column: x => x.BillOfMaterialsId,
                        principalTable: "BillsOfMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BillOfMaterialsLines_Components_ComponentId",
                        column: x => x.ComponentId,
                        principalTable: "Components",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComponentStocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComponentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComponentStocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComponentStocks_Components_ComponentId",
                        column: x => x.ComponentId,
                        principalTable: "Components",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComponentStocks_StorageLocations_StorageLocationId",
                        column: x => x.StorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComponentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TransferId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMovements_Components_ComponentId",
                        column: x => x.ComponentId,
                        principalTable: "Components",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_StorageLocations_StorageLocationId",
                        column: x => x.StorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplyNeedLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComponentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SuppliedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    SupplyNeedListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplyNeedLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplyNeedLines_Components_ComponentId",
                        column: x => x.ComponentId,
                        principalTable: "Components",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplyNeedLines_SupplyNeedLists_SupplyNeedListId",
                        column: x => x.SupplyNeedListId,
                        principalTable: "SupplyNeedLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KitDeliveryReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    RecipientPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AddressLine = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    District = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    City = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitDeliveryReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KitDeliveryReceipts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KitDeliveryReceipts_ProductUnits_ProductUnitId",
                        column: x => x.ProductUnitId,
                        principalTable: "ProductUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KitDeliveryReceipts_RentalAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "RentalAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KitDeliveryReceipts_RentalOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "RentalOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_EntityType_EntityId_OccurredAt",
                table: "AuditEntries",
                columns: new[] { "EntityType", "EntityId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BillOfMaterialsLines_BillOfMaterialsId",
                table: "BillOfMaterialsLines",
                column: "BillOfMaterialsId");

            migrationBuilder.CreateIndex(
                name: "IX_BillOfMaterialsLines_ComponentId",
                table: "BillOfMaterialsLines",
                column: "ComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_BillsOfMaterials_ProductModelId_Version",
                table: "BillsOfMaterials",
                columns: new[] { "ProductModelId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Components_DefaultStorageLocationId",
                table: "Components",
                column: "DefaultStorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Components_Sku",
                table: "Components",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComponentStocks_ComponentId_StorageLocationId",
                table: "ComponentStocks",
                columns: new[] { "ComponentId", "StorageLocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComponentStocks_StorageLocationId",
                table: "ComponentStocks",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_CustomerId",
                table: "CustomerAddresses",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Email",
                table: "Customers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveries_OccurredAt",
                table: "EmailDeliveries",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveries_Recipient_OccurredAt",
                table: "EmailDeliveries",
                columns: new[] { "Recipient", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FaultGuideEntries_IsActive_DisplayOrder",
                table: "FaultGuideEntries",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_FaultStatusEvents_FaultTicketId",
                table: "FaultStatusEvents",
                column: "FaultTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_FaultTickets_CustomerId_Status",
                table: "FaultTickets",
                columns: new[] { "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FaultTickets_Number",
                table: "FaultTickets",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionItems_ReturnInspectionId",
                table: "InspectionItems",
                column: "ReturnInspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryEvents_OwnerProductUnitId",
                table: "InventoryEvents",
                column: "OwnerProductUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryEvents_ProductUnitId",
                table: "InventoryEvents",
                column: "ProductUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_KitDeliveryReceipts_AssignmentId",
                table: "KitDeliveryReceipts",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_KitDeliveryReceipts_City_District",
                table: "KitDeliveryReceipts",
                columns: new[] { "City", "District" });

            migrationBuilder.CreateIndex(
                name: "IX_KitDeliveryReceipts_CustomerId",
                table: "KitDeliveryReceipts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_KitDeliveryReceipts_OrderId",
                table: "KitDeliveryReceipts",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_KitDeliveryReceipts_ProductUnitId_ReceivedAt",
                table: "KitDeliveryReceipts",
                columns: new[] { "ProductUnitId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_KitReturnItems_AssignmentId",
                table: "KitReturnItems",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_KitReturnItems_KitReturnRequestId",
                table: "KitReturnItems",
                column: "KitReturnRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_KitReturnItems_ProductUnitId",
                table: "KitReturnItems",
                column: "ProductUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_KitReturnRequests_CustomerId_Status",
                table: "KitReturnRequests",
                columns: new[] { "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_KitReturnRequests_TrackingNumber",
                table: "KitReturnRequests",
                column: "TrackingNumber",
                unique: true,
                filter: "[TrackingNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrderProductUnits_ProductUnitId",
                table: "OrderProductUnits",
                column: "ProductUnitId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderProductUnits_RentalOrderId",
                table: "OrderProductUnits",
                column: "RentalOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusEvents_RentalOrderId",
                table: "OrderStatusEvents",
                column: "RentalOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductModels_Sku",
                table: "ProductModels",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnits_ProductModelId",
                table: "ProductUnits",
                column: "ProductModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnits_QrCode",
                table: "ProductUnits",
                column: "QrCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnits_SerialNumber",
                table: "ProductUnits",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentalAssignments_ProductUnitId_Status",
                table: "RentalAssignments",
                columns: new[] { "ProductUnitId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RentalOrderLines_ProductModelId",
                table: "RentalOrderLines",
                column: "ProductModelId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalOrderLines_RentalOrderId",
                table: "RentalOrderLines",
                column: "RentalOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalOrders_CustomerId_Status",
                table: "RentalOrders",
                columns: new[] { "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RentalOrders_OrderNumber",
                table: "RentalOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnInspections_OrderId_ProductUnitId",
                table: "ReturnInspections",
                columns: new[] { "OrderId", "ProductUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentEvents_ShipmentId",
                table: "ShipmentEvents",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_OrderId",
                table: "Shipments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_TrackingNumber",
                table: "Shipments",
                column: "TrackingNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ComponentId_OccurredAt",
                table: "StockMovements",
                columns: new[] { "ComponentId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProductUnitId",
                table: "StockMovements",
                column: "ProductUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_StorageLocationId",
                table: "StockMovements",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_TransferId",
                table: "StockMovements",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageLocations_Code",
                table: "StorageLocations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplyNeedLines_ComponentId",
                table: "SupplyNeedLines",
                column: "ComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplyNeedLines_SupplyNeedListId",
                table: "SupplyNeedLines",
                column: "SupplyNeedListId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplyNeedLists_Status_CreatedAt",
                table: "SupplyNeedLists",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "BillOfMaterialsLines");

            migrationBuilder.DropTable(
                name: "ComponentStocks");

            migrationBuilder.DropTable(
                name: "CustomerAddresses");

            migrationBuilder.DropTable(
                name: "EmailDeliveries");

            migrationBuilder.DropTable(
                name: "FaultGuideEntries");

            migrationBuilder.DropTable(
                name: "FaultStatusEvents");

            migrationBuilder.DropTable(
                name: "InspectionItems");

            migrationBuilder.DropTable(
                name: "InventoryEvents");

            migrationBuilder.DropTable(
                name: "KitDeliveryReceipts");

            migrationBuilder.DropTable(
                name: "KitReturnItems");

            migrationBuilder.DropTable(
                name: "OrderProductUnits");

            migrationBuilder.DropTable(
                name: "OrderStatusEvents");

            migrationBuilder.DropTable(
                name: "RentalOrderLines");

            migrationBuilder.DropTable(
                name: "ShipmentEvents");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "SupplyNeedLines");

            migrationBuilder.DropTable(
                name: "BillsOfMaterials");

            migrationBuilder.DropTable(
                name: "FaultTickets");

            migrationBuilder.DropTable(
                name: "ReturnInspections");

            migrationBuilder.DropTable(
                name: "RentalAssignments");

            migrationBuilder.DropTable(
                name: "KitReturnRequests");

            migrationBuilder.DropTable(
                name: "RentalOrders");

            migrationBuilder.DropTable(
                name: "Shipments");

            migrationBuilder.DropTable(
                name: "Components");

            migrationBuilder.DropTable(
                name: "SupplyNeedLists");

            migrationBuilder.DropTable(
                name: "ProductUnits");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "StorageLocations");

            migrationBuilder.DropTable(
                name: "ProductModels");
        }
    }
}
