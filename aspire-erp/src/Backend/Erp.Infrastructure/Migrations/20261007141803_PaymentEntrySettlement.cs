using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaymentEntrySettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentStatus",
                table: "PaymentEntry",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<Guid>(
                name: "PartyId",
                table: "PaymentEntry",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "PartyType",
                table: "PaymentEntry",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "UnallocatedAmount",
                table: "PaymentEntry",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "VoucherNo",
                table: "PaymentEntry",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "SalesInvoiceId",
                table: "PaymentAllocation",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseInvoiceId",
                table: "PaymentAllocation",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentEntry_Direction",
                table: "PaymentEntry",
                sql: "([PaymentType] = 'Receive' AND [PartyType] = 'Customer') OR ([PaymentType] = 'Pay' AND [PartyType] = 'Supplier')");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocation_PurchaseInvoiceId",
                table: "PaymentAllocation",
                column: "PurchaseInvoiceId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentAllocation_Amount",
                table: "PaymentAllocation",
                sql: "[AllocatedAmount] > 0.0000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentAllocation_ExactlyOneInvoice",
                table: "PaymentAllocation",
                sql: "([SalesInvoiceId] IS NOT NULL AND [PurchaseInvoiceId] IS NULL) OR ([SalesInvoiceId] IS NULL AND [PurchaseInvoiceId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentAllocation_PurchaseInvoice",
                table: "PaymentAllocation",
                column: "PurchaseInvoiceId",
                principalTable: "PurchaseInvoice",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentAllocation_PurchaseInvoice",
                table: "PaymentAllocation");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentEntry_Direction",
                table: "PaymentEntry");

            migrationBuilder.DropIndex(
                name: "IX_PaymentAllocation_PurchaseInvoiceId",
                table: "PaymentAllocation");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentAllocation_Amount",
                table: "PaymentAllocation");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentAllocation_ExactlyOneInvoice",
                table: "PaymentAllocation");

            migrationBuilder.DropColumn(
                name: "DocumentStatus",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "PartyId",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "PartyType",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "UnallocatedAmount",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "VoucherNo",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "PurchaseInvoiceId",
                table: "PaymentAllocation");

            migrationBuilder.AlterColumn<Guid>(
                name: "SalesInvoiceId",
                table: "PaymentAllocation",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
