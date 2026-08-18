using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRentalCohortsAndProductUnitActivitiesSnapshotFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductUnitActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorDisplayNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductUnitActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductUnitActivities_ProductUnits_ProductUnitId",
                        column: x => x.ProductUnitId,
                        principalTable: "ProductUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductUnitActivities_RentalAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "RentalAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductUnitActivities_RentalOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "RentalOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RentalCohorts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalCohorts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RentalCohorts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RentalCohortStudents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RentalCohortId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    GuardianPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    AddressLine = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProductModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalCohortStudents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RentalCohortStudents_ProductModels_ProductModelId",
                        column: x => x.ProductModelId,
                        principalTable: "ProductModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RentalCohortStudents_ProductUnits_ProductUnitId",
                        column: x => x.ProductUnitId,
                        principalTable: "ProductUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RentalCohortStudents_RentalAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "RentalAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RentalCohortStudents_RentalCohorts_RentalCohortId",
                        column: x => x.RentalCohortId,
                        principalTable: "RentalCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RentalCohortStudents_RentalOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "RentalOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnitActivities_AssignmentId",
                table: "ProductUnitActivities",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnitActivities_OrderId",
                table: "ProductUnitActivities",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnitActivities_ProductUnitId_OccurredAt",
                table: "ProductUnitActivities",
                columns: new[] { "ProductUnitId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RentalCohorts_CustomerId_StartDate",
                table: "RentalCohorts",
                columns: new[] { "CustomerId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RentalCohortStudents_AssignmentId",
                table: "RentalCohortStudents",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalCohortStudents_OrderId",
                table: "RentalCohortStudents",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalCohortStudents_ProductModelId",
                table: "RentalCohortStudents",
                column: "ProductModelId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalCohortStudents_ProductUnitId",
                table: "RentalCohortStudents",
                column: "ProductUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalCohortStudents_RentalCohortId",
                table: "RentalCohortStudents",
                column: "RentalCohortId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductUnitActivities");

            migrationBuilder.DropTable(
                name: "RentalCohortStudents");

            migrationBuilder.DropTable(
                name: "RentalCohorts");
        }
    }
}
