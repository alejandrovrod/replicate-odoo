using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddPreventivePlanExecutionLogAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PreventivePlans_AssetTemplates_AssetTemplateId",
                schema: "tenant",
                table: "PreventivePlans");

            migrationBuilder.DropForeignKey(
                name: "FK_PreventivePlans_Assets_AssetId",
                schema: "tenant",
                table: "PreventivePlans");

            migrationBuilder.RenameColumn(
                name: "IntervalDays",
                schema: "tenant",
                table: "PreventivePlans",
                newName: "DueDateOffsetDays");

            migrationBuilder.AddColumn<Guid>(
                name: "PreventivePlanId",
                schema: "tenant",
                table: "WorkTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CronExpression",
                schema: "tenant",
                table: "PreventivePlans",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoAssign",
                schema: "tenant",
                table: "PreventivePlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultAssignedEmployeeId",
                schema: "tenant",
                table: "PreventivePlans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultAssignedTeamId",
                schema: "tenant",
                table: "PreventivePlans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndsAt",
                schema: "tenant",
                table: "PreventivePlans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeneratedEntityType",
                schema: "tenant",
                table: "PreventivePlans",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PreventivePlanExecutionLogs",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreventivePlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Occurrence = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedEntityType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeneratedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreventivePlanExecutionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreventivePlanExecutionLogs_PreventivePlans_PreventivePlanId",
                        column: x => x.PreventivePlanId,
                        principalSchema: "tenant",
                        principalTable: "PreventivePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_PreventivePlanId",
                schema: "tenant",
                table: "WorkTasks",
                column: "PreventivePlanId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_TenantId_PreventivePlanId",
                schema: "tenant",
                table: "WorkTasks",
                columns: new[] { "TenantId", "PreventivePlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantId_CreatedAt",
                schema: "tenant",
                table: "Notifications",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantId_UserId_IsRead",
                schema: "tenant",
                table: "Notifications",
                columns: new[] { "TenantId", "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_PreventivePlanExecutionLogs_PreventivePlanId",
                schema: "tenant",
                table: "PreventivePlanExecutionLogs",
                column: "PreventivePlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PreventivePlanExecutionLogs_TenantId_AssetId",
                schema: "tenant",
                table: "PreventivePlanExecutionLogs",
                columns: new[] { "TenantId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_PreventivePlanExecutionLogs_TenantId_PreventivePlanId_AssetId_Occurrence",
                schema: "tenant",
                table: "PreventivePlanExecutionLogs",
                columns: new[] { "TenantId", "PreventivePlanId", "AssetId", "Occurrence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreventivePlanExecutionLogs_TenantId_PreventivePlanId_Occurrence",
                schema: "tenant",
                table: "PreventivePlanExecutionLogs",
                columns: new[] { "TenantId", "PreventivePlanId", "Occurrence" });

            migrationBuilder.AddForeignKey(
                name: "FK_PreventivePlans_AssetTemplates_AssetTemplateId",
                schema: "tenant",
                table: "PreventivePlans",
                column: "AssetTemplateId",
                principalSchema: "tenant",
                principalTable: "AssetTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PreventivePlans_Assets_AssetId",
                schema: "tenant",
                table: "PreventivePlans",
                column: "AssetId",
                principalSchema: "tenant",
                principalTable: "Assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkTasks_PreventivePlans_PreventivePlanId",
                schema: "tenant",
                table: "WorkTasks",
                column: "PreventivePlanId",
                principalSchema: "tenant",
                principalTable: "PreventivePlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PreventivePlans_AssetTemplates_AssetTemplateId",
                schema: "tenant",
                table: "PreventivePlans");

            migrationBuilder.DropForeignKey(
                name: "FK_PreventivePlans_Assets_AssetId",
                schema: "tenant",
                table: "PreventivePlans");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkTasks_PreventivePlans_PreventivePlanId",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "PreventivePlanExecutionLogs",
                schema: "tenant");

            migrationBuilder.DropIndex(
                name: "IX_WorkTasks_PreventivePlanId",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropIndex(
                name: "IX_WorkTasks_TenantId_PreventivePlanId",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "PreventivePlanId",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "AutoAssign",
                schema: "tenant",
                table: "PreventivePlans");

            migrationBuilder.DropColumn(
                name: "DefaultAssignedEmployeeId",
                schema: "tenant",
                table: "PreventivePlans");

            migrationBuilder.DropColumn(
                name: "DefaultAssignedTeamId",
                schema: "tenant",
                table: "PreventivePlans");

            migrationBuilder.DropColumn(
                name: "EndsAt",
                schema: "tenant",
                table: "PreventivePlans");

            migrationBuilder.DropColumn(
                name: "GeneratedEntityType",
                schema: "tenant",
                table: "PreventivePlans");

            migrationBuilder.RenameColumn(
                name: "DueDateOffsetDays",
                schema: "tenant",
                table: "PreventivePlans",
                newName: "IntervalDays");

            migrationBuilder.AlterColumn<string>(
                name: "CronExpression",
                schema: "tenant",
                table: "PreventivePlans",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddForeignKey(
                name: "FK_PreventivePlans_AssetTemplates_AssetTemplateId",
                schema: "tenant",
                table: "PreventivePlans",
                column: "AssetTemplateId",
                principalSchema: "tenant",
                principalTable: "AssetTemplates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PreventivePlans_Assets_AssetId",
                schema: "tenant",
                table: "PreventivePlans",
                column: "AssetId",
                principalSchema: "tenant",
                principalTable: "Assets",
                principalColumn: "Id");
        }
    }
}
