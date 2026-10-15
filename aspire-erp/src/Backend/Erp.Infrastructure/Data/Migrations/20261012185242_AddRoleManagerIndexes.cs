using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleManagerIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Roles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "DocType",
                table: "DocTypePermissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_Tenant_Role",
                table: "UserRoles",
                columns: new[] { "TenantId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_Tenant_User",
                table: "UserRoles",
                columns: new[] { "TenantId", "UserId" });

            // Limpiar duplicados previos antes de crear los índices únicos
            migrationBuilder.Sql(@"
                ;WITH CTE_DocPerms AS (
                    SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId, RoleId, DocType ORDER BY Id) AS rn
                    FROM dbo.DocTypePermissions
                )
                DELETE FROM CTE_DocPerms WHERE rn > 1;

                ;WITH CTE_Roles AS (
                    SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId, Name ORDER BY Id) AS rn
                    FROM dbo.Roles
                )
                DELETE FROM CTE_Roles WHERE rn > 1;

                ;WITH CTE_UserRoles AS (
                    SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId, UserId, RoleId ORDER BY Id) AS rn
                    FROM dbo.UserRoles
                )
                DELETE FROM CTE_UserRoles WHERE rn > 1;
            ");

            migrationBuilder.CreateIndex(
                name: "UQ_UserRoles_Tenant_User_Role",
                table: "UserRoles",
                columns: new[] { "TenantId", "UserId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Roles_Tenant_Name",
                table: "Roles",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_DocPerms_Tenant_Role_DocType",
                table: "DocTypePermissions",
                columns: new[] { "TenantId", "RoleId", "DocType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserRoles_Tenant_Role",
                table: "UserRoles");

            migrationBuilder.DropIndex(
                name: "IX_UserRoles_Tenant_User",
                table: "UserRoles");

            migrationBuilder.DropIndex(
                name: "UQ_UserRoles_Tenant_User_Role",
                table: "UserRoles");

            migrationBuilder.DropIndex(
                name: "UQ_Roles_Tenant_Name",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "UQ_DocPerms_Tenant_Role_DocType",
                table: "DocTypePermissions");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Roles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "DocType",
                table: "DocTypePermissions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }
    }
}
