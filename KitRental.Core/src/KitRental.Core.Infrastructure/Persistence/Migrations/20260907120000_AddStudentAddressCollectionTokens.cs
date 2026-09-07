using KitRental.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(KitRentalDbContext))]
    [Migration("20260907120000_AddStudentAddressCollectionTokens")]
    public partial class AddStudentAddressCollectionTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AddressSubmittedAt",
                table: "RentalCohortStudents",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicAddressToken",
                table: "RentalCohortStudents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE RentalCohortStudents
                SET PublicAddressToken = REPLACE(CONVERT(nvarchar(36), NEWID()), '-', '')
                WHERE PublicAddressToken = ''
                """);

            migrationBuilder.CreateIndex(
                name: "IX_RentalCohortStudents_PublicAddressToken",
                table: "RentalCohortStudents",
                column: "PublicAddressToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RentalCohortStudents_PublicAddressToken",
                table: "RentalCohortStudents");

            migrationBuilder.DropColumn(
                name: "AddressSubmittedAt",
                table: "RentalCohortStudents");

            migrationBuilder.DropColumn(
                name: "PublicAddressToken",
                table: "RentalCohortStudents");
        }
    }
}
