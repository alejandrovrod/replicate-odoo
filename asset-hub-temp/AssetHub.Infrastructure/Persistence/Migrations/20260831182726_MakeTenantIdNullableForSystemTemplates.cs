using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeTenantIdNullableForSystemTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BusinessEntityTypes_Code_TenantId",
                schema: "tenant",
                table: "BusinessEntityTypes");

            migrationBuilder.DropIndex(
                name: "IX_AssetTemplates_Code_Version_TenantId",
                schema: "tenant",
                table: "AssetTemplates");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                schema: "tenant",
                table: "BusinessEntityTypes",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                schema: "tenant",
                table: "AssetTemplates",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessEntityTypes_Code_TenantId",
                schema: "tenant",
                table: "BusinessEntityTypes",
                columns: new[] { "Code", "TenantId" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTemplates_Code_Version_TenantId",
                schema: "tenant",
                table: "AssetTemplates",
                columns: new[] { "Code", "Version", "TenantId" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BusinessEntityTypes_Code_TenantId",
                schema: "tenant",
                table: "BusinessEntityTypes");

            migrationBuilder.DropIndex(
                name: "IX_AssetTemplates_Code_Version_TenantId",
                schema: "tenant",
                table: "AssetTemplates");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                schema: "tenant",
                table: "BusinessEntityTypes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                schema: "tenant",
                table: "AssetTemplates",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessEntityTypes_Code_TenantId",
                schema: "tenant",
                table: "BusinessEntityTypes",
                columns: new[] { "Code", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetTemplates_Code_Version_TenantId",
                schema: "tenant",
                table: "AssetTemplates",
                columns: new[] { "Code", "Version", "TenantId" },
                unique: true);
        }
    }
}
