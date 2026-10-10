using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddWorkTaskSoftDeleteAndTaskCommentsConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkTasks_Employees_AssignedEmployeeId",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkTasks_Incidents_IncidentId",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkTasks_MaintenanceOrders_MaintenanceOrderId",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkTasks_TaskRecurrences_TaskRecurrenceId",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkTasks_Teams_AssignedTeamId",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropIndex(
                name: "IX_WorkTasks_TenantId_State",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "tenant",
                table: "WorkTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "tenant",
                table: "WorkTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_TenantId_AssignedTeamId",
                schema: "tenant",
                table: "WorkTasks",
                columns: new[] { "TenantId", "AssignedTeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_TenantId_DueAt",
                schema: "tenant",
                table: "WorkTasks",
                columns: new[] { "TenantId", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_TenantId_IncidentId",
                schema: "tenant",
                table: "WorkTasks",
                columns: new[] { "TenantId", "IncidentId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_TenantId_IsDeleted",
                schema: "tenant",
                table: "WorkTasks",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_TenantId_MaintenanceOrderId",
                schema: "tenant",
                table: "WorkTasks",
                columns: new[] { "TenantId", "MaintenanceOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_TenantId_State_IsDeleted",
                schema: "tenant",
                table: "WorkTasks",
                columns: new[] { "TenantId", "State", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_TenantId_TaskRecurrenceId",
                schema: "tenant",
                table: "WorkTasks",
                columns: new[] { "TenantId", "TaskRecurrenceId" });

            migrationBuilder.AddForeignKey(
                name: "FK_WorkTasks_Employees_AssignedEmployeeId",
                schema: "tenant",
                table: "WorkTasks",
                column: "AssignedEmployeeId",
                principalSchema: "tenant",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkTasks_Incidents_IncidentId",
                schema: "tenant",
                table: "WorkTasks",
                column: "IncidentId",
                principalSchema: "tenant",
                principalTable: "Incidents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkTasks_MaintenanceOrders_MaintenanceOrderId",
                schema: "tenant",
                table: "WorkTasks",
                column: "MaintenanceOrderId",
                principalSchema: "tenant",
                principalTable: "MaintenanceOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkTasks_TaskRecurrences_TaskRecurrenceId",
                schema: "tenant",
                table: "WorkTasks",
                column: "TaskRecurrenceId",
                principalSchema: "tenant",
                principalTable: "TaskRecurrences",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkTasks_Teams_AssignedTeamId",
                schema: "tenant",
                table: "WorkTasks",
                column: "AssignedTeamId",
                principalSchema: "tenant",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkTasks_Employees_AssignedEmployeeId",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkTasks_Incidents_IncidentId",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkTasks_MaintenanceOrders_MaintenanceOrderId",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkTasks_TaskRecurrences_TaskRecurrenceId",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkTasks_Teams_AssignedTeamId",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropIndex(
                name: "IX_WorkTasks_TenantId_AssignedTeamId",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropIndex(
                name: "IX_WorkTasks_TenantId_DueAt",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropIndex(
                name: "IX_WorkTasks_TenantId_IncidentId",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropIndex(
                name: "IX_WorkTasks_TenantId_IsDeleted",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropIndex(
                name: "IX_WorkTasks_TenantId_MaintenanceOrderId",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropIndex(
                name: "IX_WorkTasks_TenantId_State_IsDeleted",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropIndex(
                name: "IX_WorkTasks_TenantId_TaskRecurrenceId",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "tenant",
                table: "WorkTasks");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_TenantId_State",
                schema: "tenant",
                table: "WorkTasks",
                columns: new[] { "TenantId", "State" });

            migrationBuilder.AddForeignKey(
                name: "FK_WorkTasks_Employees_AssignedEmployeeId",
                schema: "tenant",
                table: "WorkTasks",
                column: "AssignedEmployeeId",
                principalSchema: "tenant",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkTasks_Incidents_IncidentId",
                schema: "tenant",
                table: "WorkTasks",
                column: "IncidentId",
                principalSchema: "tenant",
                principalTable: "Incidents",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkTasks_MaintenanceOrders_MaintenanceOrderId",
                schema: "tenant",
                table: "WorkTasks",
                column: "MaintenanceOrderId",
                principalSchema: "tenant",
                principalTable: "MaintenanceOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkTasks_TaskRecurrences_TaskRecurrenceId",
                schema: "tenant",
                table: "WorkTasks",
                column: "TaskRecurrenceId",
                principalSchema: "tenant",
                principalTable: "TaskRecurrences",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkTasks_Teams_AssignedTeamId",
                schema: "tenant",
                table: "WorkTasks",
                column: "AssignedTeamId",
                principalSchema: "tenant",
                principalTable: "Teams",
                principalColumn: "Id");
        }
    }
}
