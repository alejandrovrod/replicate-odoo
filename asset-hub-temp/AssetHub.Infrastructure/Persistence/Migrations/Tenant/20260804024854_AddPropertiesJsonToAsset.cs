using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddPropertiesJsonToAsset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "tenant",
                table: "Assets",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "PropertiesJson",
                schema: "tenant",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "tenant",
                table: "Assets",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "tenant",
                table: "AssetAttachments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "tenant",
                table: "AssetAttachments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "tenant",
                table: "AssetAttachments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_AssetAttachments_TenantId_AssetId",
                schema: "tenant",
                table: "AssetAttachments",
                columns: new[] { "TenantId", "AssetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AssetAttachments_TenantId_AssetId",
                schema: "tenant",
                table: "AssetAttachments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "tenant",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "PropertiesJson",
                schema: "tenant",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "tenant",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "tenant",
                table: "AssetAttachments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "tenant",
                table: "AssetAttachments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "tenant",
                table: "AssetAttachments");
        }
    }
}
