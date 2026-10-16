using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxEngineAndTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaxTemplateItem_Account_AccountId",
                table: "TaxTemplateItem");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "TaxTemplates",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDefault",
                table: "TaxTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "TaxTemplates",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWSEQUENTIALID()",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "TaxTemplates",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Sales");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "TaxTemplateItem",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWSEQUENTIALID()",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "CalculationBasis",
                table: "TaxTemplateItem",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "OnNetTotal");

            migrationBuilder.AddColumn<string>(
                name: "ChargeType",
                table: "TaxTemplateItem",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Add");

            migrationBuilder.AddColumn<decimal>(
                name: "BaseAmount",
                table: "SalesInvoiceTax",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CalculationBasis",
                table: "SalesInvoiceTax",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "OnNetTotal");

            migrationBuilder.AddColumn<string>(
                name: "ChargeType",
                table: "SalesInvoiceTax",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Add");

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceTax",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PurchaseInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                    ChargeType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Add"),
                    CalculationBasis = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "OnNetTotal"),
                    FixedAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoiceTax", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceTax_PurchaseInvoice",
                        column: x => x.PurchaseInvoiceId,
                        principalTable: "PurchaseInvoice",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceTax_TaxAccount",
                        column: x => x.AccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_TaxTemplate_Tenant_Company_Category_Title",
                table: "TaxTemplates",
                columns: new[] { "TenantId", "CompanyId", "Category", "Title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceTax_AccountId",
                table: "PurchaseInvoiceTax",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceTax_PurchaseInvoiceId",
                table: "PurchaseInvoiceTax",
                column: "PurchaseInvoiceId");

            // Tax-engine Fase 2: TaxTotal is the signed tax sum (negative on retention-heavy
            // bills), so the sign-consistency CHECK moves to Net/Grand only.
            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseInvoice_Totals",
                table: "PurchaseInvoice");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseInvoice_Totals",
                table: "PurchaseInvoice",
                sql: "([NetTotal] >= 0.0000 AND [GrandTotal] >= 0.0000) OR ([NetTotal] <= 0.0000 AND [GrandTotal] <= 0.0000)");

            migrationBuilder.AddForeignKey(
                name: "FK_TaxTemplateItem_Account_AccountId",
                table: "TaxTemplateItem",
                column: "AccountId",
                principalTable: "Account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaxTemplateItem_Account_AccountId",
                table: "TaxTemplateItem");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseInvoice_Totals",
                table: "PurchaseInvoice");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseInvoice_Totals",
                table: "PurchaseInvoice",
                sql: "([NetTotal] >= 0.0000 AND [TaxTotal] >= 0.0000 AND [GrandTotal] >= 0.0000) "
                    + "OR ([NetTotal] <= 0.0000 AND [TaxTotal] <= 0.0000 AND [GrandTotal] <= 0.0000)");

            migrationBuilder.DropTable(
                name: "PurchaseInvoiceTax");

            migrationBuilder.DropIndex(
                name: "UQ_TaxTemplate_Tenant_Company_Category_Title",
                table: "TaxTemplates");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "TaxTemplates");

            migrationBuilder.DropColumn(
                name: "CalculationBasis",
                table: "TaxTemplateItem");

            migrationBuilder.DropColumn(
                name: "ChargeType",
                table: "TaxTemplateItem");

            migrationBuilder.DropColumn(
                name: "BaseAmount",
                table: "SalesInvoiceTax");

            migrationBuilder.DropColumn(
                name: "CalculationBasis",
                table: "SalesInvoiceTax");

            migrationBuilder.DropColumn(
                name: "ChargeType",
                table: "SalesInvoiceTax");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "TaxTemplates",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDefault",
                table: "TaxTemplates",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "TaxTemplates",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "NEWSEQUENTIALID()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "TaxTemplateItem",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "NEWSEQUENTIALID()");

            migrationBuilder.AddForeignKey(
                name: "FK_TaxTemplateItem_Account_AccountId",
                table: "TaxTemplateItem",
                column: "AccountId",
                principalTable: "Account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
