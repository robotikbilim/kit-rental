using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRentalCohortStudentCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "RentalCohortStudents",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "RentalCohortStudents",
                type: "float",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentalCohortStudents_Latitude_Longitude",
                table: "RentalCohortStudents",
                columns: new[] { "Latitude", "Longitude" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RentalCohortStudents_Latitude_Longitude",
                table: "RentalCohortStudents");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "RentalCohortStudents");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "RentalCohortStudents");
        }
    }
}
