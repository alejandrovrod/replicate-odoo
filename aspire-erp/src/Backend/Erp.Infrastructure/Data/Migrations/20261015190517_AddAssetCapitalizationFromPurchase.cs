using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetCapitalizationFromPurchase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssetCategoryId",
                table: "Item",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFixedAsset",
                table: "Item",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultSalvageValuePercent",
                table: "AssetCategory",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DefaultUsefulLifeMonths",
                table: "AssetCategory",
                type: "int",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<Guid>(
                name: "OpeningJournalEntryId",
                table: "Asset",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseInvoiceId",
                table: "Asset",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseInvoiceItemId",
                table: "Asset",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Item_AssetCategoryId",
                table: "Item",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Asset_OpeningJournalEntryId",
                table: "Asset",
                column: "OpeningJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Asset_PurchaseInvoiceId",
                table: "Asset",
                column: "PurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Asset_PurchaseInvoiceItemId",
                table: "Asset",
                column: "PurchaseInvoiceItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_Asset_OpeningJournalEntry",
                table: "Asset",
                column: "OpeningJournalEntryId",
                principalTable: "JournalEntry",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Asset_PurchaseInvoice",
                table: "Asset",
                column: "PurchaseInvoiceId",
                principalTable: "PurchaseInvoice",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Asset_PurchaseInvoiceItem",
                table: "Asset",
                column: "PurchaseInvoiceItemId",
                principalTable: "PurchaseInvoiceLine",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Item_AssetCategory",
                table: "Item",
                column: "AssetCategoryId",
                principalTable: "AssetCategory",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Asset_OpeningJournalEntry",
                table: "Asset");

            migrationBuilder.DropForeignKey(
                name: "FK_Asset_PurchaseInvoice",
                table: "Asset");

            migrationBuilder.DropForeignKey(
                name: "FK_Asset_PurchaseInvoiceItem",
                table: "Asset");

            migrationBuilder.DropForeignKey(
                name: "FK_Item_AssetCategory",
                table: "Item");

            migrationBuilder.DropIndex(
                name: "IX_Item_AssetCategoryId",
                table: "Item");

            migrationBuilder.DropIndex(
                name: "IX_Asset_OpeningJournalEntryId",
                table: "Asset");

            migrationBuilder.DropIndex(
                name: "IX_Asset_PurchaseInvoiceId",
                table: "Asset");

            migrationBuilder.DropIndex(
                name: "IX_Asset_PurchaseInvoiceItemId",
                table: "Asset");

            migrationBuilder.DropColumn(
                name: "AssetCategoryId",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "IsFixedAsset",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "DefaultSalvageValuePercent",
                table: "AssetCategory");

            migrationBuilder.DropColumn(
                name: "DefaultUsefulLifeMonths",
                table: "AssetCategory");

            migrationBuilder.DropColumn(
                name: "OpeningJournalEntryId",
                table: "Asset");

            migrationBuilder.DropColumn(
                name: "PurchaseInvoiceId",
                table: "Asset");

            migrationBuilder.DropColumn(
                name: "PurchaseInvoiceItemId",
                table: "Asset");
        }
    }
}
