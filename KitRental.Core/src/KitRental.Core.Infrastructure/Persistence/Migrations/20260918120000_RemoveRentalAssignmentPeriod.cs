using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using KitRental.Core.Infrastructure.Persistence;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations;

[DbContext(typeof(KitRentalDbContext))]
[Migration("20260918120000_RemoveRentalAssignmentPeriod")]
public partial class RemoveRentalAssignmentPeriod : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Period",
            table: "RentalAssignments");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Period",
            table: "RentalAssignments",
            type: "nvarchar(21)",
            maxLength: 21,
            nullable: false,
            defaultValue: "0001-01-01|0001-01-01");
    }
}
