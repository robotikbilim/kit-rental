using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceKitDeliveryReceiptsWithLocationEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KitLocationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContactName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AddressLine = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    District = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    City = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitLocationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KitLocationEvents_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KitLocationEvents_ProductUnits_ProductUnitId",
                        column: x => x.ProductUnitId,
                        principalTable: "ProductUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KitLocationEvents_RentalAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "RentalAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KitLocationEvents_RentalOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "RentalOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KitLocationEvents_AssignmentId",
                table: "KitLocationEvents",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_KitLocationEvents_City_District",
                table: "KitLocationEvents",
                columns: new[] { "City", "District" });

            migrationBuilder.CreateIndex(
                name: "IX_KitLocationEvents_CustomerId",
                table: "KitLocationEvents",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_KitLocationEvents_OrderId",
                table: "KitLocationEvents",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_KitLocationEvents_ProductUnitId_OccurredAt",
                table: "KitLocationEvents",
                columns: new[] { "ProductUnitId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_KitLocationEvents_Source_SourceId",
                table: "KitLocationEvents",
                columns: new[] { "Source", "SourceId" });

            migrationBuilder.Sql("""
                INSERT INTO KitLocationEvents (
                    Id,
                    ProductUnitId,
                    AssignmentId,
                    OrderId,
                    CustomerId,
                    Source,
                    SourceId,
                    ContactName,
                    ContactPhone,
                    AddressLine,
                    District,
                    City,
                    Latitude,
                    Longitude,
                    OccurredAt,
                    ActorId)
                SELECT
                    NEWID(),
                    ProductUnitId,
                    AssignmentId,
                    OrderId,
                    CustomerId,
                    1,
                    Id,
                    RecipientName,
                    RecipientPhone,
                    AddressLine,
                    District,
                    City,
                    Latitude,
                    Longitude,
                    ReceivedAt,
                    ActorId
                FROM KitDeliveryReceipts;
                """);

            migrationBuilder.DropTable(
                name: "KitDeliveryReceipts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KitLocationEvents");

            migrationBuilder.CreateTable(
                name: "KitDeliveryReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressLine = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    City = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    District = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    RecipientPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
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
        }
    }
}
