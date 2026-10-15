using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseInvoiceLine_Amount_NonNegative",
                table: "PurchaseInvoiceLine");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseInvoiceLine_Qty_Positive",
                table: "PurchaseInvoiceLine");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseInvoice_Totals",
                table: "PurchaseInvoice");

            migrationBuilder.AddColumn<bool>(
                name: "IsReturn",
                table: "PurchaseInvoice",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ReturnAgainstId",
                table: "PurchaseInvoice",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UpdateStock",
                table: "PurchaseInvoice",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseInvoiceLine_Amount_NonNegative",
                table: "PurchaseInvoiceLine",
                sql: "([Qty] > 0 AND [Amount] >= 0) OR ([Qty] < 0 AND [Amount] <= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseInvoiceLine_Qty_Positive",
                table: "PurchaseInvoiceLine",
                sql: "[Qty] <> 0");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoice_ReturnAgainstId",
                table: "PurchaseInvoice",
                column: "ReturnAgainstId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseInvoice_Totals",
                table: "PurchaseInvoice",
                sql: "([NetTotal] >= 0.0000 AND [TaxTotal] >= 0.0000 AND [GrandTotal] >= 0.0000) OR ([NetTotal] <= 0.0000 AND [TaxTotal] <= 0.0000 AND [GrandTotal] <= 0.0000)");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoice_ReturnAgainst",
                table: "PurchaseInvoice",
                column: "ReturnAgainstId",
                principalTable: "PurchaseInvoice",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoice_ReturnAgainst",
                table: "PurchaseInvoice");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseInvoiceLine_Amount_NonNegative",
                table: "PurchaseInvoiceLine");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseInvoiceLine_Qty_Positive",
                table: "PurchaseInvoiceLine");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoice_ReturnAgainstId",
                table: "PurchaseInvoice");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseInvoice_Totals",
                table: "PurchaseInvoice");

            migrationBuilder.DropColumn(
                name: "IsReturn",
                table: "PurchaseInvoice");

            migrationBuilder.DropColumn(
                name: "ReturnAgainstId",
                table: "PurchaseInvoice");

            migrationBuilder.DropColumn(
                name: "UpdateStock",
                table: "PurchaseInvoice");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseInvoiceLine_Amount_NonNegative",
                table: "PurchaseInvoiceLine",
                sql: "[Amount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseInvoiceLine_Qty_Positive",
                table: "PurchaseInvoiceLine",
                sql: "[Qty] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseInvoice_Totals",
                table: "PurchaseInvoice",
                sql: "[NetTotal] >= 0.0000 AND [TaxTotal] >= 0.0000 AND [GrandTotal] >= 0.0000");
        }
    }
}
