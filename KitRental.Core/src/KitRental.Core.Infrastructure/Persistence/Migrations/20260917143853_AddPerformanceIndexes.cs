using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ProductUnitId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_StorageLocationId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_ProductUnitActivities_AssignmentId",
                table: "ProductUnitActivities");

            migrationBuilder.DropIndex(
                name: "IX_ProductUnitActivities_OrderId",
                table: "ProductUnitActivities");

            migrationBuilder.DropIndex(
                name: "IX_KitLocationEvents_OrderId",
                table: "KitLocationEvents");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProductUnitId_OccurredAt",
                table: "StockMovements",
                columns: new[] { "ProductUnitId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_StorageLocationId_OccurredAt",
                table: "StockMovements",
                columns: new[] { "StorageLocationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnInspections_ProductUnitId",
                table: "ReturnInspections",
                column: "ProductUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalCohortStudents_OrderId_IsDeleted",
                table: "RentalCohortStudents",
                columns: new[] { "OrderId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnitActivities_AssignmentId_OccurredAt",
                table: "ProductUnitActivities",
                columns: new[] { "AssignmentId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnitActivities_OrderId_OccurredAt",
                table: "ProductUnitActivities",
                columns: new[] { "OrderId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_KitLocationEvents_OrderId_OccurredAt",
                table: "KitLocationEvents",
                columns: new[] { "OrderId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FaultTickets_AssignmentId_Status",
                table: "FaultTickets",
                columns: new[] { "AssignmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FaultTickets_OrderId_Status",
                table: "FaultTickets",
                columns: new[] { "OrderId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ProductUnitId_OccurredAt",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_StorageLocationId_OccurredAt",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_ReturnInspections_ProductUnitId",
                table: "ReturnInspections");

            migrationBuilder.DropIndex(
                name: "IX_RentalCohortStudents_OrderId_IsDeleted",
                table: "RentalCohortStudents");

            migrationBuilder.DropIndex(
                name: "IX_ProductUnitActivities_AssignmentId_OccurredAt",
                table: "ProductUnitActivities");

            migrationBuilder.DropIndex(
                name: "IX_ProductUnitActivities_OrderId_OccurredAt",
                table: "ProductUnitActivities");

            migrationBuilder.DropIndex(
                name: "IX_KitLocationEvents_OrderId_OccurredAt",
                table: "KitLocationEvents");

            migrationBuilder.DropIndex(
                name: "IX_FaultTickets_AssignmentId_Status",
                table: "FaultTickets");

            migrationBuilder.DropIndex(
                name: "IX_FaultTickets_OrderId_Status",
                table: "FaultTickets");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProductUnitId",
                table: "StockMovements",
                column: "ProductUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_StorageLocationId",
                table: "StockMovements",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnitActivities_AssignmentId",
                table: "ProductUnitActivities",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnitActivities_OrderId",
                table: "ProductUnitActivities",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_KitLocationEvents_OrderId",
                table: "KitLocationEvents",
                column: "OrderId");
        }
    }
}
