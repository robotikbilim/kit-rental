using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddListPaginationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KitLocationEvents_AssignmentId",
                table: "KitLocationEvents");

            migrationBuilder.DropIndex(
                name: "IX_KitLocationEvents_CustomerId",
                table: "KitLocationEvents");

            migrationBuilder.DropIndex(
                name: "IX_FaultGuideEntries_ProductModelId",
                table: "FaultGuideEntries");

            migrationBuilder.CreateIndex(
                name: "IX_RentalOrders_CustomerId_CreatedAt",
                table: "RentalOrders",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RentalOrders_Status_CreatedAt",
                table: "RentalOrders",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RentalCohorts_CustomerId_CreatedAt",
                table: "RentalCohorts",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RentalAssignments_CreatedAt",
                table: "RentalAssignments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RentalAssignments_CustomerId_Status",
                table: "RentalAssignments",
                columns: new[] { "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RentalAssignments_OrderLineId",
                table: "RentalAssignments",
                column: "OrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnits_ProductModelId_Status",
                table: "ProductUnits",
                columns: new[] { "ProductModelId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnits_Status",
                table: "ProductUnits",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProductModels_Name",
                table: "ProductModels",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_KitReturnRequests_CreatedAt",
                table: "KitReturnRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_KitReturnRequests_Status_CreatedAt",
                table: "KitReturnRequests",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_KitLocationEvents_AssignmentId_OccurredAt",
                table: "KitLocationEvents",
                columns: new[] { "AssignmentId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_KitLocationEvents_CustomerId_OccurredAt",
                table: "KitLocationEvents",
                columns: new[] { "CustomerId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_KitLocationEvents_Latitude_Longitude",
                table: "KitLocationEvents",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_FaultTickets_Origin_OpenedAt",
                table: "FaultTickets",
                columns: new[] { "Origin", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FaultTickets_ProductUnitId",
                table: "FaultTickets",
                column: "ProductUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_FaultTickets_Severity_OpenedAt",
                table: "FaultTickets",
                columns: new[] { "Severity", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FaultTickets_Status_OpenedAt",
                table: "FaultTickets",
                columns: new[] { "Status", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FaultGuideEntries_ProductModelId_IsActive_DisplayOrder",
                table: "FaultGuideEntries",
                columns: new[] { "ProductModelId", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_IsActive",
                table: "Customers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Name",
                table: "Customers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Components_Name",
                table: "Components",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_Action_OccurredAt",
                table: "AuditEntries",
                columns: new[] { "Action", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_ActorId",
                table: "AuditEntries",
                column: "ActorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RentalOrders_CustomerId_CreatedAt",
                table: "RentalOrders");

            migrationBuilder.DropIndex(
                name: "IX_RentalOrders_Status_CreatedAt",
                table: "RentalOrders");

            migrationBuilder.DropIndex(
                name: "IX_RentalCohorts_CustomerId_CreatedAt",
                table: "RentalCohorts");

            migrationBuilder.DropIndex(
                name: "IX_RentalAssignments_CreatedAt",
                table: "RentalAssignments");

            migrationBuilder.DropIndex(
                name: "IX_RentalAssignments_CustomerId_Status",
                table: "RentalAssignments");

            migrationBuilder.DropIndex(
                name: "IX_RentalAssignments_OrderLineId",
                table: "RentalAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ProductUnits_ProductModelId_Status",
                table: "ProductUnits");

            migrationBuilder.DropIndex(
                name: "IX_ProductUnits_Status",
                table: "ProductUnits");

            migrationBuilder.DropIndex(
                name: "IX_ProductModels_Name",
                table: "ProductModels");

            migrationBuilder.DropIndex(
                name: "IX_KitReturnRequests_CreatedAt",
                table: "KitReturnRequests");

            migrationBuilder.DropIndex(
                name: "IX_KitReturnRequests_Status_CreatedAt",
                table: "KitReturnRequests");

            migrationBuilder.DropIndex(
                name: "IX_KitLocationEvents_AssignmentId_OccurredAt",
                table: "KitLocationEvents");

            migrationBuilder.DropIndex(
                name: "IX_KitLocationEvents_CustomerId_OccurredAt",
                table: "KitLocationEvents");

            migrationBuilder.DropIndex(
                name: "IX_KitLocationEvents_Latitude_Longitude",
                table: "KitLocationEvents");

            migrationBuilder.DropIndex(
                name: "IX_FaultTickets_Origin_OpenedAt",
                table: "FaultTickets");

            migrationBuilder.DropIndex(
                name: "IX_FaultTickets_ProductUnitId",
                table: "FaultTickets");

            migrationBuilder.DropIndex(
                name: "IX_FaultTickets_Severity_OpenedAt",
                table: "FaultTickets");

            migrationBuilder.DropIndex(
                name: "IX_FaultTickets_Status_OpenedAt",
                table: "FaultTickets");

            migrationBuilder.DropIndex(
                name: "IX_FaultGuideEntries_ProductModelId_IsActive_DisplayOrder",
                table: "FaultGuideEntries");

            migrationBuilder.DropIndex(
                name: "IX_Customers_IsActive",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Name",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Components_Name",
                table: "Components");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_Action_OccurredAt",
                table: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_ActorId",
                table: "AuditEntries");

            migrationBuilder.CreateIndex(
                name: "IX_KitLocationEvents_AssignmentId",
                table: "KitLocationEvents",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_KitLocationEvents_CustomerId",
                table: "KitLocationEvents",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_FaultGuideEntries_ProductModelId",
                table: "FaultGuideEntries",
                column: "ProductModelId");
        }
    }
}
