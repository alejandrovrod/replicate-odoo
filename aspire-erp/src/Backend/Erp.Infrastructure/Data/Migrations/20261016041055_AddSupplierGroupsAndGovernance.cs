using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierGroupsAndGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultPriceListId",
                table: "Supplier",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HoldType",
                table: "Supplier",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFrozen",
                table: "Supplier",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OnHold",
                table: "Supplier",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentTermsTemplateId",
                table: "Supplier",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReleaseDate",
                table: "Supplier",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierGroupId",
                table: "Supplier",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SupplierCompanyAccount",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayableAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdvanceAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierCompanyAccount", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierCompanyAccount_AdvanceAccount",
                        column: x => x.AdvanceAccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierCompanyAccount_Company",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierCompanyAccount_PayableAccount",
                        column: x => x.PayableAccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierCompanyAccount_Supplier",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierGroup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierGroup", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Supplier_SupplierGroupId",
                table: "Supplier",
                column: "SupplierGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Supplier_Tenant_Group",
                table: "Supplier",
                columns: new[] { "TenantId", "SupplierGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCompanyAccount_AdvanceAccountId",
                table: "SupplierCompanyAccount",
                column: "AdvanceAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCompanyAccount_CompanyId",
                table: "SupplierCompanyAccount",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCompanyAccount_PayableAccountId",
                table: "SupplierCompanyAccount",
                column: "PayableAccountId");

            migrationBuilder.CreateIndex(
                name: "UQ_SupplierCompanyAccount_Supplier_Company",
                table: "SupplierCompanyAccount",
                columns: new[] { "SupplierId", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_SupplierGroup_Tenant_Code",
                table: "SupplierGroup",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Supplier_SupplierGroup",
                table: "Supplier",
                column: "SupplierGroupId",
                principalTable: "SupplierGroup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Supplier_SupplierGroup",
                table: "Supplier");

            migrationBuilder.DropTable(
                name: "SupplierCompanyAccount");

            migrationBuilder.DropTable(
                name: "SupplierGroup");

            migrationBuilder.DropIndex(
                name: "IX_Supplier_SupplierGroupId",
                table: "Supplier");

            migrationBuilder.DropIndex(
                name: "IX_Supplier_Tenant_Group",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "DefaultPriceListId",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "HoldType",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "IsFrozen",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "OnHold",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "PaymentTermsTemplateId",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "ReleaseDate",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "SupplierGroupId",
                table: "Supplier");
        }
    }
}
