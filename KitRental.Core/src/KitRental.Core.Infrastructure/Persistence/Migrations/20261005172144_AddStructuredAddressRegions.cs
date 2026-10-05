using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredAddressRegions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryCity",
                table: "RentalOrders",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DeliveryCityId",
                table: "RentalOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryDistrict",
                table: "RentalOrders",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DeliveryDistrictId",
                table: "RentalOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "RentalCohortStudents",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CityId",
                table: "RentalCohortStudents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "RentalCohortStudents",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DistrictId",
                table: "RentalCohortStudents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "KitReturnRequests",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CityId",
                table: "KitReturnRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "KitReturnRequests",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DistrictId",
                table: "KitReturnRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "KitLocationEvents",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CityId",
                table: "KitLocationEvents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "KitLocationEvents",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DistrictId",
                table: "KitLocationEvents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "FaultTickets",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CityId",
                table: "FaultTickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "FaultTickets",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DistrictId",
                table: "FaultTickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "FaultKargonomiShipments",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CityId",
                table: "FaultKargonomiShipments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "FaultKargonomiShipments",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DistrictId",
                table: "FaultKargonomiShipments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "CustomerAddresses",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CityId",
                table: "CustomerAddresses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "CustomerAddresses",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DistrictId",
                table: "CustomerAddresses",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryCity",
                table: "RentalOrders");

            migrationBuilder.DropColumn(
                name: "DeliveryCityId",
                table: "RentalOrders");

            migrationBuilder.DropColumn(
                name: "DeliveryDistrict",
                table: "RentalOrders");

            migrationBuilder.DropColumn(
                name: "DeliveryDistrictId",
                table: "RentalOrders");

            migrationBuilder.DropColumn(
                name: "City",
                table: "RentalCohortStudents");

            migrationBuilder.DropColumn(
                name: "CityId",
                table: "RentalCohortStudents");

            migrationBuilder.DropColumn(
                name: "District",
                table: "RentalCohortStudents");

            migrationBuilder.DropColumn(
                name: "DistrictId",
                table: "RentalCohortStudents");

            migrationBuilder.DropColumn(
                name: "City",
                table: "KitReturnRequests");

            migrationBuilder.DropColumn(
                name: "CityId",
                table: "KitReturnRequests");

            migrationBuilder.DropColumn(
                name: "District",
                table: "KitReturnRequests");

            migrationBuilder.DropColumn(
                name: "DistrictId",
                table: "KitReturnRequests");

            migrationBuilder.DropColumn(
                name: "City",
                table: "KitLocationEvents");

            migrationBuilder.DropColumn(
                name: "CityId",
                table: "KitLocationEvents");

            migrationBuilder.DropColumn(
                name: "District",
                table: "KitLocationEvents");

            migrationBuilder.DropColumn(
                name: "DistrictId",
                table: "KitLocationEvents");

            migrationBuilder.DropColumn(
                name: "City",
                table: "FaultTickets");

            migrationBuilder.DropColumn(
                name: "CityId",
                table: "FaultTickets");

            migrationBuilder.DropColumn(
                name: "District",
                table: "FaultTickets");

            migrationBuilder.DropColumn(
                name: "DistrictId",
                table: "FaultTickets");

            migrationBuilder.DropColumn(
                name: "City",
                table: "FaultKargonomiShipments");

            migrationBuilder.DropColumn(
                name: "CityId",
                table: "FaultKargonomiShipments");

            migrationBuilder.DropColumn(
                name: "District",
                table: "FaultKargonomiShipments");

            migrationBuilder.DropColumn(
                name: "DistrictId",
                table: "FaultKargonomiShipments");

            migrationBuilder.DropColumn(
                name: "City",
                table: "CustomerAddresses");

            migrationBuilder.DropColumn(
                name: "CityId",
                table: "CustomerAddresses");

            migrationBuilder.DropColumn(
                name: "District",
                table: "CustomerAddresses");

            migrationBuilder.DropColumn(
                name: "DistrictId",
                table: "CustomerAddresses");
        }
    }
}
