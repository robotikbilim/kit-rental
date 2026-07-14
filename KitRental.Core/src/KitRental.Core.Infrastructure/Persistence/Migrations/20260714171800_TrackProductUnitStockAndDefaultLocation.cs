using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrackProductUnitStockAndDefaultLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultForNewComponents",
                table: "StorageLocations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductUnitId",
                table: "StockMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProductUnitId",
                table: "StockMovements",
                column: "ProductUnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ProductUnitId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "IsDefaultForNewComponents",
                table: "StorageLocations");

            migrationBuilder.DropColumn(
                name: "ProductUnitId",
                table: "StockMovements");
        }
    }
}
