using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomFieldsAndExtensibilityEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomData",
                table: "Supplier",
                type: "NVARCHAR(MAX)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomData",
                table: "SalesInvoice",
                type: "NVARCHAR(MAX)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomData",
                table: "PurchaseInvoice",
                type: "NVARCHAR(MAX)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomData",
                table: "Item",
                type: "NVARCHAR(MAX)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomData",
                table: "Customer",
                type: "NVARCHAR(MAX)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomData",
                table: "Asset",
                type: "NVARCHAR(MAX)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomFieldDefinition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FieldType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DefaultValue = table.Column<string>(type: "NVARCHAR(MAX)", nullable: true),
                    ValidationJsonSchema = table.Column<string>(type: "NVARCHAR(MAX)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldDefinition", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_CustomFieldDefinition_Tenant_Entity_Field",
                table: "CustomFieldDefinition",
                columns: new[] { "TenantId", "EntityName", "FieldName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomFieldDefinition");

            migrationBuilder.DropColumn(
                name: "CustomData",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "CustomData",
                table: "SalesInvoice");

            migrationBuilder.DropColumn(
                name: "CustomData",
                table: "PurchaseInvoice");

            migrationBuilder.DropColumn(
                name: "CustomData",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "CustomData",
                table: "Customer");

            migrationBuilder.DropColumn(
                name: "CustomData",
                table: "Asset");
        }
    }
}
