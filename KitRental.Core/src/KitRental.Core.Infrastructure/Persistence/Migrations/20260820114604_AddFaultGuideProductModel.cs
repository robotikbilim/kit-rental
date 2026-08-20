using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFaultGuideProductModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductModelId",
                table: "FaultGuideEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FaultGuideEntries_ProductModelId",
                table: "FaultGuideEntries",
                column: "ProductModelId");

            migrationBuilder.AddForeignKey(
                name: "FK_FaultGuideEntries_ProductModels_ProductModelId",
                table: "FaultGuideEntries",
                column: "ProductModelId",
                principalTable: "ProductModels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FaultGuideEntries_ProductModels_ProductModelId",
                table: "FaultGuideEntries");

            migrationBuilder.DropIndex(
                name: "IX_FaultGuideEntries_ProductModelId",
                table: "FaultGuideEntries");

            migrationBuilder.DropColumn(
                name: "ProductModelId",
                table: "FaultGuideEntries");
        }
    }
}
