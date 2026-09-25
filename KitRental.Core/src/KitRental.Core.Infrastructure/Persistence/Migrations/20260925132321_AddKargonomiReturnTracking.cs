using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKargonomiReturnTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExternalShipmentId",
                table: "KitReturnRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalStatus",
                table: "KitReturnRequests",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalStatusLabel",
                table: "KitReturnRequests",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExternalUpdatedAt",
                table: "KitReturnRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KargonomiBarcode",
                table: "KitReturnRequests",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KitReturnRequests_ExternalShipmentId",
                table: "KitReturnRequests",
                column: "ExternalShipmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KitReturnRequests_ExternalShipmentId",
                table: "KitReturnRequests");

            migrationBuilder.DropColumn(
                name: "ExternalShipmentId",
                table: "KitReturnRequests");

            migrationBuilder.DropColumn(
                name: "ExternalStatus",
                table: "KitReturnRequests");

            migrationBuilder.DropColumn(
                name: "ExternalStatusLabel",
                table: "KitReturnRequests");

            migrationBuilder.DropColumn(
                name: "ExternalUpdatedAt",
                table: "KitReturnRequests");

            migrationBuilder.DropColumn(
                name: "KargonomiBarcode",
                table: "KitReturnRequests");
        }
    }
}
