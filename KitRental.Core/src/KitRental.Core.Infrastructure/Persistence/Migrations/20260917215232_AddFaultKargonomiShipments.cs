using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFaultKargonomiShipments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FaultKargonomiShipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FaultTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    RecipientPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RecipientAddress = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ExternalShipmentId = table.Column<int>(type: "int", nullable: true),
                    Carrier = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TrackingNumber = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ExternalStatus = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    StatusLabel = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaultKargonomiShipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaultKargonomiShipments_FaultTickets_FaultTicketId",
                        column: x => x.FaultTicketId,
                        principalTable: "FaultTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FaultKargonomiShipmentEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalStatus = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StatusLabel = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    TrackingNumber = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FaultKargonomiShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaultKargonomiShipmentEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaultKargonomiShipmentEvents_FaultKargonomiShipments_FaultKargonomiShipmentId",
                        column: x => x.FaultKargonomiShipmentId,
                        principalTable: "FaultKargonomiShipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FaultKargonomiShipmentEvents_FaultKargonomiShipmentId",
                table: "FaultKargonomiShipmentEvents",
                column: "FaultKargonomiShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_FaultKargonomiShipments_ExternalShipmentId",
                table: "FaultKargonomiShipments",
                column: "ExternalShipmentId",
                unique: true,
                filter: "[ExternalShipmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FaultKargonomiShipments_FaultTicketId_Direction",
                table: "FaultKargonomiShipments",
                columns: new[] { "FaultTicketId", "Direction" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FaultKargonomiShipmentEvents");

            migrationBuilder.DropTable(
                name: "FaultKargonomiShipments");
        }
    }
}
