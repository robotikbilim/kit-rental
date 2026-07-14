using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplyNeedLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "SupplyNeedLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComponentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
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
                name: "SupplyNeedLines");

            migrationBuilder.DropTable(
                name: "SupplyNeedLists");
        }
    }
}
