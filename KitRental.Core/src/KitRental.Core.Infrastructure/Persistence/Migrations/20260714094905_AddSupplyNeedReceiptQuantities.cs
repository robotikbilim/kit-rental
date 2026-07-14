using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplyNeedReceiptQuantities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SuppliedQuantity",
                table: "SupplyNeedLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SuppliedQuantity",
                table: "SupplyNeedLines");
        }
    }
}
