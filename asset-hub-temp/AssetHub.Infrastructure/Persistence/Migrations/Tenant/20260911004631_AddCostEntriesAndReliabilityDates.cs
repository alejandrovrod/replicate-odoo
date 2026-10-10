using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCostEntriesAndReliabilityDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FailureOccurredAt",
                schema: "tenant",
                table: "MaintenanceOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RepairStartedAt",
                schema: "tenant",
                table: "MaintenanceOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CostEntries",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CostType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsEstimated = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostEntries_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostEntries_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalSchema: "tenant",
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostEntries_MaintenanceOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalSchema: "tenant",
                        principalTable: "MaintenanceOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_AssetId",
                schema: "tenant",
                table: "CostEntries",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_IncidentId",
                schema: "tenant",
                table: "CostEntries",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_TenantId_AssetId_OccurredAt",
                schema: "tenant",
                table: "CostEntries",
                columns: new[] { "TenantId", "AssetId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_TenantId_IncidentId",
                schema: "tenant",
                table: "CostEntries",
                columns: new[] { "TenantId", "IncidentId" });

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_TenantId_WorkOrderId",
                schema: "tenant",
                table: "CostEntries",
                columns: new[] { "TenantId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_WorkOrderId",
                schema: "tenant",
                table: "CostEntries",
                column: "WorkOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CostEntries",
                schema: "tenant");

            migrationBuilder.DropColumn(
                name: "FailureOccurredAt",
                schema: "tenant",
                table: "MaintenanceOrders");

            migrationBuilder.DropColumn(
                name: "RepairStartedAt",
                schema: "tenant",
                table: "MaintenanceOrders");
        }
    }
}
