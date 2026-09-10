using Microsoft.EntityFrameworkCore.Migrations;
using KitRental.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations;

[Migration("20260910100000_AddFaultAttachmentUrl")]
[DbContext(typeof(KitRentalDbContext))]
public partial class AddFaultAttachmentUrl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AttachmentUrl",
            table: "FaultTickets",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AttachmentUrl", table: "FaultTickets");
    }
}
