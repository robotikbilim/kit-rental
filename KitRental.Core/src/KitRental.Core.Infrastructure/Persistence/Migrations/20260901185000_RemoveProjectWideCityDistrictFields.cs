using KitRental.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(KitRentalDbContext))]
    [Migration("20260901185000_RemoveProjectWideCityDistrictFields")]
    public partial class RemoveProjectWideCityDistrictFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KitLocationEvents_City_District",
                table: "KitLocationEvents");

            migrationBuilder.DropColumn(
                name: "City",
                table: "KitLocationEvents");

            migrationBuilder.DropColumn(
                name: "District",
                table: "KitLocationEvents");

            migrationBuilder.DropColumn(
                name: "DeliveryCity",
                table: "RentalOrders");

            migrationBuilder.DropColumn(
                name: "DeliveryDistrict",
                table: "RentalOrders");

            migrationBuilder.DropColumn(
                name: "City",
                table: "CustomerAddresses");

            migrationBuilder.DropColumn(
                name: "District",
                table: "CustomerAddresses");

            migrationBuilder.DropTable(
                name: "LocationDistricts");

            migrationBuilder.DropTable(
                name: "LocationCities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LocationCities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationCities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocationDistricts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CityId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationDistricts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationDistricts_LocationCities_CityId",
                        column: x => x.CityId,
                        principalTable: "LocationCities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "KitLocationEvents",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "Bilinmiyor");

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "KitLocationEvents",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "Bilinmiyor");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryCity",
                table: "RentalOrders",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "Bilinmiyor");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryDistrict",
                table: "RentalOrders",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "Bilinmiyor");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "CustomerAddresses",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "Bilinmiyor");

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "CustomerAddresses",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "Bilinmiyor");

            migrationBuilder.CreateIndex(
                name: "IX_LocationCities_Code",
                table: "LocationCities",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocationDistricts_CityId_Name",
                table: "LocationDistricts",
                columns: new[] { "CityId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KitLocationEvents_City_District",
                table: "KitLocationEvents",
                columns: new[] { "City", "District" });
        }
    }
}
