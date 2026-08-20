using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations;

[DbContext(typeof(KitRentalDbContext))]
[Migration("20260820140000_AddRentalCohortStudentLocationFields")]
public partial class AddRentalCohortStudentLocationFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "City",
            table: "RentalCohortStudents",
            type: "nvarchar(120)",
            maxLength: 120,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "District",
            table: "RentalCohortStudents",
            type: "nvarchar(120)",
            maxLength: 120,
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "City", table: "RentalCohortStudents");
        migrationBuilder.DropColumn(name: "District", table: "RentalCohortStudents");
    }
}
