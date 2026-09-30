using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIyzicoPwiModelAlignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KitOwnershipPayments_ProductUnitId",
                table: "KitOwnershipPayments");

            migrationBuilder.RenameIndex(
                name: "IX_KitOwnershipPayments_ProductUnitId1",
                table: "KitOwnershipPayments",
                newName: "IX_KitOwnershipPayments_ProductUnitId");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_KitOwnershipPayments_ProductUnitId",
                table: "KitOwnershipPayments",
                newName: "IX_KitOwnershipPayments_ProductUnitId1");

            migrationBuilder.CreateIndex(
                name: "IX_KitOwnershipPayments_ProductUnitId",
                table: "KitOwnershipPayments",
                column: "ProductUnitId",
                unique: true,
                filter: "[Status] IN (1, 2, 3)");

        }
    }
}
