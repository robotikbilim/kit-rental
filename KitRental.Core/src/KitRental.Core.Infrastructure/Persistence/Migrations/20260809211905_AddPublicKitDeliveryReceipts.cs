using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicKitDeliveryReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KitDeliveryReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientFirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecipientLastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecipientPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AddressLine = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    District = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    City = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KitDeliveryReceipts");
        }
    }
}
