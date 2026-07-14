using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddComponentDefaultStorageLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultStorageLocationId",
                table: "Components",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Components_DefaultStorageLocationId",
                table: "Components",
                column: "DefaultStorageLocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Components_StorageLocations_DefaultStorageLocationId",
                table: "Components",
                column: "DefaultStorageLocationId",
                principalTable: "StorageLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Components_StorageLocations_DefaultStorageLocationId",
                table: "Components");

            migrationBuilder.DropIndex(
                name: "IX_Components_DefaultStorageLocationId",
                table: "Components");

            migrationBuilder.DropColumn(
                name: "DefaultStorageLocationId",
                table: "Components");
        }
    }
}
