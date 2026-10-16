using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchAndSerialTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "StockLedgerEntry",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "StockEntryItem",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "SerialNos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Active",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "SerialNumber",
                table: "SerialNos",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "SerialNos",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "SerialNos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWSEQUENTIALID()",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "StockEntryItemId",
                table: "SerialNos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "PurchaseReceiptLine",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasBatchNo",
                table: "Item",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasSerialNo",
                table: "Item",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "DeliveryNoteLine",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Batches",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "BatchId",
                table: "Batches",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "Batches",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWSEQUENTIALID()",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<bool>(
                name: "IsDisabled",
                table: "Batches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_StockLedgerEntry_BatchId",
                table: "StockLedgerEntry",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockEntryItem_BatchId",
                table: "StockEntryItem",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "UQ_SerialNo_Tenant_Number",
                table: "SerialNos",
                columns: new[] { "TenantId", "SerialNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceiptLine_BatchId",
                table: "PurchaseReceiptLine",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteLine_BatchId",
                table: "DeliveryNoteLine",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "UQ_Batch_Tenant_Item_Number",
                table: "Batches",
                columns: new[] { "TenantId", "ItemId", "BatchId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryNoteLine_Batch",
                table: "DeliveryNoteLine",
                column: "BatchId",
                principalTable: "Batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReceiptLine_Batch",
                table: "PurchaseReceiptLine",
                column: "BatchId",
                principalTable: "Batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockEntryItem_Batch",
                table: "StockEntryItem",
                column: "BatchId",
                principalTable: "Batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockLedgerEntry_Batch",
                table: "StockLedgerEntry",
                column: "BatchId",
                principalTable: "Batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryNoteLine_Batch",
                table: "DeliveryNoteLine");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReceiptLine_Batch",
                table: "PurchaseReceiptLine");

            migrationBuilder.DropForeignKey(
                name: "FK_StockEntryItem_Batch",
                table: "StockEntryItem");

            migrationBuilder.DropForeignKey(
                name: "FK_StockLedgerEntry_Batch",
                table: "StockLedgerEntry");

            migrationBuilder.DropIndex(
                name: "IX_StockLedgerEntry_BatchId",
                table: "StockLedgerEntry");

            migrationBuilder.DropIndex(
                name: "IX_StockEntryItem_BatchId",
                table: "StockEntryItem");

            migrationBuilder.DropIndex(
                name: "UQ_SerialNo_Tenant_Number",
                table: "SerialNos");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReceiptLine_BatchId",
                table: "PurchaseReceiptLine");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryNoteLine_BatchId",
                table: "DeliveryNoteLine");

            migrationBuilder.DropIndex(
                name: "UQ_Batch_Tenant_Item_Number",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "StockLedgerEntry");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "StockEntryItem");

            migrationBuilder.DropColumn(
                name: "StockEntryItemId",
                table: "SerialNos");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "PurchaseReceiptLine");

            migrationBuilder.DropColumn(
                name: "HasBatchNo",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "HasSerialNo",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "DeliveryNoteLine");

            migrationBuilder.DropColumn(
                name: "IsDisabled",
                table: "Batches");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "SerialNos",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "Active");

            migrationBuilder.AlterColumn<string>(
                name: "SerialNumber",
                table: "SerialNos",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "SerialNos",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldDefaultValueSql: "SYSDATETIMEOFFSET()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "SerialNos",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "NEWSEQUENTIALID()");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Batches",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldDefaultValueSql: "SYSDATETIMEOFFSET()");

            migrationBuilder.AlterColumn<string>(
                name: "BatchId",
                table: "Batches",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "Batches",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "NEWSEQUENTIALID()");
        }
    }
}
