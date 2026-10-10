using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddMaintenanceOrderAssignedEmployeeNavigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceOrders_AssignedEmployeeId",
                schema: "tenant",
                table: "MaintenanceOrders",
                column: "AssignedEmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceOrders_Employees_AssignedEmployeeId",
                schema: "tenant",
                table: "MaintenanceOrders",
                column: "AssignedEmployeeId",
                principalSchema: "tenant",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceOrders_Employees_AssignedEmployeeId",
                schema: "tenant",
                table: "MaintenanceOrders");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceOrders_AssignedEmployeeId",
                schema: "tenant",
                table: "MaintenanceOrders");
        }
    }
}
