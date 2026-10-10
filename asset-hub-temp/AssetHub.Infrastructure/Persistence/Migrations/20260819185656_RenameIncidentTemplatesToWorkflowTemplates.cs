using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameIncidentTemplatesToWorkflowTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_IncidentTemplates_IncidentTemplateId",
                schema: "tenant",
                table: "Incidents");

            migrationBuilder.RenameTable(
                name: "IncidentTemplates",
                schema: "tenant",
                newName: "WorkflowTemplates", newSchema: "tenant");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                schema: "tenant",
                table: "WorkflowTemplates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "incident");

            migrationBuilder.RenameColumn(
                name: "IncidentTemplateId",
                schema: "tenant",
                table: "Incidents",
                newName: "WorkflowTemplateId");

            migrationBuilder.RenameIndex(
                name: "IX_Incidents_IncidentTemplateId",
                schema: "tenant",
                table: "Incidents",
                newName: "IX_Incidents_WorkflowTemplateId");

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowTemplateId",
                schema: "tenant",
                table: "MaintenanceOrders",
                type: "uniqueidentifier",
                nullable: true);

            // CreateTable removed as it is replaced by RenameTable + AddColumn

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTemplates_Code_Version_TenantId",
                schema: "tenant",
                table: "WorkflowTemplates",
                columns: new[] { "Code", "Version", "TenantId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_WorkflowTemplates_WorkflowTemplateId",
                schema: "tenant",
                table: "Incidents",
                column: "WorkflowTemplateId",
                principalSchema: "tenant",
                principalTable: "WorkflowTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_WorkflowTemplates_WorkflowTemplateId",
                schema: "tenant",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "tenant",
                table: "WorkflowTemplates");

            migrationBuilder.RenameTable(
                name: "WorkflowTemplates",
                schema: "tenant",
                newName: "IncidentTemplates", newSchema: "tenant");

            migrationBuilder.DropColumn(
                name: "WorkflowTemplateId",
                schema: "tenant",
                table: "MaintenanceOrders");

            migrationBuilder.RenameColumn(
                name: "WorkflowTemplateId",
                schema: "tenant",
                table: "Incidents",
                newName: "IncidentTemplateId");

            migrationBuilder.RenameIndex(
                name: "IX_Incidents_WorkflowTemplateId",
                schema: "tenant",
                table: "Incidents",
                newName: "IX_Incidents_IncidentTemplateId");

            // CreateTable removed as it is replaced by RenameTable + DropColumn

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTemplates_Code_Version_TenantId",
                schema: "tenant",
                table: "IncidentTemplates",
                columns: new[] { "Code", "Version", "TenantId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_IncidentTemplates_IncidentTemplateId",
                schema: "tenant",
                table: "Incidents",
                column: "IncidentTemplateId",
                principalSchema: "tenant",
                principalTable: "IncidentTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
