using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations;

public partial class AddKitOwnershipPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "KitOwnershipPayments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProductUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                PaidPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                ConversationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                BasketId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                IyzicoTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                ProtectedIyzicoToken = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                PaymentPageUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                ProviderStatus = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                FraudStatus = table.Column<int>(type: "int", nullable: true),
                PaymentId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                PaymentTransactionId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                LastWebhookEventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                LastWebhookPaymentId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                LastWebhookStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                LastWebhookReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LastWebhookProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_KitOwnershipPayments", x => x.Id);
                table.ForeignKey("FK_KitOwnershipPayments_Customers_CustomerId", x => x.CustomerId, "Customers", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_KitOwnershipPayments_ProductUnits_ProductUnitId", x => x.ProductUnitId, "ProductUnits", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_KitOwnershipPayments_RentalAssignments_AssignmentId", x => x.AssignmentId, "RentalAssignments", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_KitOwnershipPayments_AssignmentId", "KitOwnershipPayments", "AssignmentId");
        migrationBuilder.CreateIndex("IX_KitOwnershipPayments_ConversationId", "KitOwnershipPayments", "ConversationId", unique: true);
        migrationBuilder.CreateIndex("IX_KitOwnershipPayments_CustomerId_CreatedAt", "KitOwnershipPayments", new[] { "CustomerId", "CreatedAt" });
        migrationBuilder.CreateIndex("IX_KitOwnershipPayments_IyzicoTokenHash", "KitOwnershipPayments", "IyzicoTokenHash", unique: true, filter: "[IyzicoTokenHash] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_KitOwnershipPayments_ProductUnitId_Status_ExpiresAt", "KitOwnershipPayments", new[] { "ProductUnitId", "Status", "ExpiresAt" });
        migrationBuilder.CreateIndex("IX_KitOwnershipPayments_ProductUnitId", "KitOwnershipPayments", "ProductUnitId", unique: true, filter: "[Status] IN (1, 2, 3)");
        migrationBuilder.CreateIndex("IX_KitOwnershipPayments_ProductUnitId1", "KitOwnershipPayments", "ProductUnitId", unique: true, filter: "[Status] = 4");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("KitOwnershipPayments");
}
