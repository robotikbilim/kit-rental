using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceManualShipmentWithKargonomi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipmentEvents");

            migrationBuilder.DropTable(
                name: "Shipments");

            migrationBuilder.CreateTable(
                name: "KargonomiShipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalShipmentId = table.Column<int>(type: "int", nullable: true),
                    Carrier = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TrackingNumber = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ExternalStatus = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    StatusLabel = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    BarcodeBase64 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KargonomiShipments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KargonomiShipmentEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalStatus = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StatusLabel = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    TrackingNumber = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    KargonomiShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KargonomiShipmentEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KargonomiShipmentEvents_KargonomiShipments_KargonomiShipmentId",
                        column: x => x.KargonomiShipmentId,
                        principalTable: "KargonomiShipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KargonomiShipmentEvents_KargonomiShipmentId",
                table: "KargonomiShipmentEvents",
                column: "KargonomiShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_KargonomiShipmentEvents_OccurredAt_ExternalStatus",
                table: "KargonomiShipmentEvents",
                columns: new[] { "OccurredAt", "ExternalStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_KargonomiShipments_ExternalShipmentId",
                table: "KargonomiShipments",
                column: "ExternalShipmentId",
                unique: true,
                filter: "[ExternalShipmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KargonomiShipments_OrderId_StudentId",
                table: "KargonomiShipments",
                columns: new[] { "OrderId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KargonomiShipments_State",
                table: "KargonomiShipments",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KargonomiShipmentEvents");

            migrationBuilder.DropTable(
                name: "KargonomiShipments");

            migrationBuilder.CreateTable(
                name: "Shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Carrier = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    FaultTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TrackingNumber = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
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
        }
    }
}
