using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicFaultAndDeliveryCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "KitDeliveryReceipts",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "KitDeliveryReceipts",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "FaultTickets",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "FaultTickets",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "KitDeliveryReceipts");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "KitDeliveryReceipts");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "FaultTickets");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "FaultTickets");
        }
    }
}
