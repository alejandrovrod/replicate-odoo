using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Infrastructure.Persistence.Migrations.Security
{
    /// <inheritdoc />
    public partial class AddRolePermissionMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_RolePermissions",
                schema: "security",
                table: "RolePermissions");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "security",
                table: "RolePermissions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "security",
                table: "Permissions",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Module",
                schema: "security",
                table: "Permissions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PlanModule",
                schema: "security",
                table: "Permissions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "security",
                table: "AspNetRoles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemDefault",
                schema: "security",
                table: "AspNetRoles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_RolePermissions",
                schema: "security",
                table: "RolePermissions",
                columns: new[] { "TenantId", "RoleId", "PermissionId" });

            migrationBuilder.CreateTable(
                name: "PermissionAssignmentAudits",
                schema: "security",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Granted = table.Column<bool>(type: "bit", nullable: false),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Ip = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionAssignmentAudits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_TenantId_RoleId",
                schema: "security",
                table: "RolePermissions",
                columns: new[] { "TenantId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                schema: "security",
                table: "Permissions",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PermissionAssignmentAudits",
                schema: "security");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RolePermissions",
                schema: "security",
                table: "RolePermissions");

            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_TenantId_RoleId",
                schema: "security",
                table: "RolePermissions");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_Code",
                schema: "security",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "security",
                table: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "Module",
                schema: "security",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "PlanModule",
                schema: "security",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "security",
                table: "AspNetRoles");

            migrationBuilder.DropColumn(
                name: "IsSystemDefault",
                schema: "security",
                table: "AspNetRoles");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "security",
                table: "Permissions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RolePermissions",
                schema: "security",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" });
        }
    }
}
