using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkMaintenanceWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE tenant.Incidents SET PriorityId = NULL WHERE PriorityId IS NOT NULL AND PriorityId NOT IN (SELECT Id FROM tenant.CatalogItems)");
            migrationBuilder.Sql("DELETE FROM tenant.Incidents WHERE TypeId NOT IN (SELECT Id FROM tenant.CatalogItems)");

            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_Assets_AssetId",
                schema: "tenant",
                table: "Incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_IncidentTemplates_IncidentTemplateId",
                schema: "tenant",
                table: "Incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceOrders_Assets_AssetId",
                schema: "tenant",
                table: "MaintenanceOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceOrders_Incidents_IncidentId",
                schema: "tenant",
                table: "MaintenanceOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceOrders_PreventivePlans_PreventivePlanId",
                schema: "tenant",
                table: "MaintenanceOrders");

            migrationBuilder.CreateIndex(
                name: "IX_PreventivePlans_DefaultAssignedEmployeeId",
                schema: "tenant",
                table: "PreventivePlans",
                column: "DefaultAssignedEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PreventivePlans_DefaultAssignedTeamId",
                schema: "tenant",
                table: "PreventivePlans",
                column: "DefaultAssignedTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_PriorityId",
                schema: "tenant",
                table: "Incidents",
                column: "PriorityId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_TypeId",
                schema: "tenant",
                table: "Incidents",
                column: "TypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_Assets_AssetId",
                schema: "tenant",
                table: "Incidents",
                column: "AssetId",
                principalSchema: "tenant",
                principalTable: "Assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_CatalogItems_PriorityId",
                schema: "tenant",
                table: "Incidents",
                column: "PriorityId",
                principalSchema: "tenant",
                principalTable: "CatalogItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_CatalogItems_TypeId",
                schema: "tenant",
                table: "Incidents",
                column: "TypeId",
                principalSchema: "tenant",
                principalTable: "CatalogItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_IncidentTemplates_IncidentTemplateId",
                schema: "tenant",
                table: "Incidents",
                column: "IncidentTemplateId",
                principalSchema: "tenant",
                principalTable: "IncidentTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceOrders_Assets_AssetId",
                schema: "tenant",
                table: "MaintenanceOrders",
                column: "AssetId",
                principalSchema: "tenant",
                principalTable: "Assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceOrders_Incidents_IncidentId",
                schema: "tenant",
                table: "MaintenanceOrders",
                column: "IncidentId",
                principalSchema: "tenant",
                principalTable: "Incidents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceOrders_PreventivePlans_PreventivePlanId",
                schema: "tenant",
                table: "MaintenanceOrders",
                column: "PreventivePlanId",
                principalSchema: "tenant",
                principalTable: "PreventivePlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PreventivePlans_Employees_DefaultAssignedEmployeeId",
                schema: "tenant",
                table: "PreventivePlans",
                column: "DefaultAssignedEmployeeId",
                principalSchema: "tenant",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PreventivePlans_Teams_DefaultAssignedTeamId",
                schema: "tenant",
                table: "PreventivePlans",
                column: "DefaultAssignedTeamId",
                principalSchema: "tenant",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_Assets_AssetId",
                schema: "tenant",
                table: "Incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_CatalogItems_PriorityId",
                schema: "tenant",
                table: "Incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_CatalogItems_TypeId",
                schema: "tenant",
                table: "Incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_IncidentTemplates_IncidentTemplateId",
                schema: "tenant",
                table: "Incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceOrders_Assets_AssetId",
                schema: "tenant",
                table: "MaintenanceOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceOrders_Incidents_IncidentId",
                schema: "tenant",
                table: "MaintenanceOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceOrders_PreventivePlans_PreventivePlanId",
                schema: "tenant",
                table: "MaintenanceOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PreventivePlans_Employees_DefaultAssignedEmployeeId",
                schema: "tenant",
                table: "PreventivePlans");

            migrationBuilder.DropForeignKey(
                name: "FK_PreventivePlans_Teams_DefaultAssignedTeamId",
                schema: "tenant",
                table: "PreventivePlans");

            migrationBuilder.DropIndex(
                name: "IX_PreventivePlans_DefaultAssignedEmployeeId",
                schema: "tenant",
                table: "PreventivePlans");

            migrationBuilder.DropIndex(
                name: "IX_PreventivePlans_DefaultAssignedTeamId",
                schema: "tenant",
                table: "PreventivePlans");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_PriorityId",
                schema: "tenant",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_TypeId",
                schema: "tenant",
                table: "Incidents");

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_Assets_AssetId",
                schema: "tenant",
                table: "Incidents",
                column: "AssetId",
                principalSchema: "tenant",
                principalTable: "Assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_IncidentTemplates_IncidentTemplateId",
                schema: "tenant",
                table: "Incidents",
                column: "IncidentTemplateId",
                principalSchema: "tenant",
                principalTable: "IncidentTemplates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceOrders_Assets_AssetId",
                schema: "tenant",
                table: "MaintenanceOrders",
                column: "AssetId",
                principalSchema: "tenant",
                principalTable: "Assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceOrders_Incidents_IncidentId",
                schema: "tenant",
                table: "MaintenanceOrders",
                column: "IncidentId",
                principalSchema: "tenant",
                principalTable: "Incidents",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceOrders_PreventivePlans_PreventivePlanId",
                schema: "tenant",
                table: "MaintenanceOrders",
                column: "PreventivePlanId",
                principalSchema: "tenant",
                principalTable: "PreventivePlans",
                principalColumn: "Id");
        }
    }
}
