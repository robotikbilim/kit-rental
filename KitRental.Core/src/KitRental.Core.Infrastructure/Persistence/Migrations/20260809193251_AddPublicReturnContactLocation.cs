using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicReturnContactLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "KitReturnRequests",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "KitReturnRequests",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequesterFirstName",
                table: "KitReturnRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequesterLastName",
                table: "KitReturnRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequesterPhone",
                table: "KitReturnRequests",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "KitReturnRequests");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "KitReturnRequests");

            migrationBuilder.DropColumn(
                name: "RequesterFirstName",
                table: "KitReturnRequests");

            migrationBuilder.DropColumn(
                name: "RequesterLastName",
                table: "KitReturnRequests");

            migrationBuilder.DropColumn(
                name: "RequesterPhone",
                table: "KitReturnRequests");
        }
    }
}
