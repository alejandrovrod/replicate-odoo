using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowTemplateToPreventivePlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowTemplateId",
                schema: "tenant",
                table: "PreventivePlans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreventivePlans_WorkflowTemplateId",
                schema: "tenant",
                table: "PreventivePlans",
                column: "WorkflowTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_PreventivePlans_WorkflowTemplates_WorkflowTemplateId",
                schema: "tenant",
                table: "PreventivePlans",
                column: "WorkflowTemplateId",
                principalSchema: "tenant",
                principalTable: "WorkflowTemplates",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PreventivePlans_WorkflowTemplates_WorkflowTemplateId",
                schema: "tenant",
                table: "PreventivePlans");

            migrationBuilder.DropIndex(
                name: "IX_PreventivePlans_WorkflowTemplateId",
                schema: "tenant",
                table: "PreventivePlans");

            migrationBuilder.DropColumn(
                name: "WorkflowTemplateId",
                schema: "tenant",
                table: "PreventivePlans");
        }
    }
}
