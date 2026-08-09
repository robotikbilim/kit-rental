using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFaultGuideEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FaultGuideEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Problem = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Solution = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaultGuideEntries", x => x.Id);
                });

            var createdAt = new DateTimeOffset(new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc));
            migrationBuilder.InsertData(
                table: "FaultGuideEntries",
                columns: new[] { "Id", "Title", "Problem", "Solution", "DisplayOrder", "IsActive", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    {
                        new Guid("7a1d02d8-d40d-4f58-9a89-c4790cb1d901"),
                        "Kit acilmiyor veya enerji almiyor",
                        "Kitin uzerindeki kart, motor ya da sensorler hic calismiyor; LED yanmiyor.",
                        "Pil veya adaptoru kontrol edin. Kabloyu kart uzerindeki dogru girise takip tekrar deneyin. Varsa acma kapama anahtarini kapatip 10 saniye sonra tekrar acin.",
                        10, true, createdAt, createdAt
                    },
                    {
                        new Guid("f05a6fc8-622f-462c-b537-1641f910fd87"),
                        "Baglanti kablosu ya da parca gevsek",
                        "Kit bazen calisiyor bazen duruyor; hareket ettirince sorun degisiyor.",
                        "Gevsek jumper/kablo olup olmadigini kontrol edin. Parcalari zorlamadan yerine oturtun. Kablo ucu kirik veya egriyse ariza kaydi olusturun.",
                        20, true, createdAt, createdAt
                    },
                    {
                        new Guid("98c10957-7e3b-4607-9b1f-1a8f3ba0f61e"),
                        "Program yuklenmiyor",
                        "Bilgisayar kiti gormuyor veya yukleme sirasinda hata veriyor.",
                        "USB kablosunu cikarip tekrar takin, farkli USB girisi deneyin. Dogru kart/port secili oldugundan emin olun. Hata devam ederse ekran goruntusuyle ariza kaydi olusturun.",
                        30, true, createdAt, createdAt
                    }
                });

            migrationBuilder.CreateIndex(
                name: "IX_FaultGuideEntries_IsActive_DisplayOrder",
                table: "FaultGuideEntries",
                columns: new[] { "IsActive", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FaultGuideEntries");
        }
    }
}
