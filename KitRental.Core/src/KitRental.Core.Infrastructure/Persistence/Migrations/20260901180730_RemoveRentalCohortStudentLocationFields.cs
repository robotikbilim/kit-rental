using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRentalCohortStudentLocationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RentalCohortStudents_LocationCities_CityId",
                table: "RentalCohortStudents");

            migrationBuilder.DropForeignKey(
                name: "FK_RentalCohortStudents_LocationDistricts_DistrictId",
                table: "RentalCohortStudents");

            migrationBuilder.DropIndex(
                name: "IX_RentalCohortStudents_CityId",
                table: "RentalCohortStudents");

            migrationBuilder.DropIndex(
                name: "IX_RentalCohortStudents_DistrictId",
                table: "RentalCohortStudents");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "RentalCohortStudents",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CityId",
                table: "RentalCohortStudents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "RentalCohortStudents",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DistrictId",
                table: "RentalCohortStudents",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentalCohortStudents_CityId",
                table: "RentalCohortStudents",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalCohortStudents_DistrictId",
                table: "RentalCohortStudents",
                column: "DistrictId");

            migrationBuilder.AddForeignKey(
                name: "FK_RentalCohortStudents_LocationCities_CityId",
                table: "RentalCohortStudents",
                column: "CityId",
                principalTable: "LocationCities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RentalCohortStudents_LocationDistricts_DistrictId",
                table: "RentalCohortStudents",
                column: "DistrictId",
                principalTable: "LocationDistricts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
