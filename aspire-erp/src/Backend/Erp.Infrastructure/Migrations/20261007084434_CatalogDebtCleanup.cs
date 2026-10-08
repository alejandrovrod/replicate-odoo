using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CatalogDebtCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Supplier_Account_DefaultPayableAccountId1",
                table: "Supplier");

            migrationBuilder.DropIndex(
                name: "IX_Supplier_DefaultPayableAccountId1",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "DefaultPayableAccountId1",
                table: "Supplier");

            migrationBuilder.AddColumn<Guid>(
                name: "ExpenseAccountId",
                table: "Item",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IncomeAccountId",
                table: "Item",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AssetCategory",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Item_ExpenseAccountId",
                table: "Item",
                column: "ExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Item_IncomeAccountId",
                table: "Item",
                column: "IncomeAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Item_ExpenseAccount",
                table: "Item",
                column: "ExpenseAccountId",
                principalTable: "Account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Item_IncomeAccount",
                table: "Item",
                column: "IncomeAccountId",
                principalTable: "Account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Item_ExpenseAccount",
                table: "Item");

            migrationBuilder.DropForeignKey(
                name: "FK_Item_IncomeAccount",
                table: "Item");

            migrationBuilder.DropIndex(
                name: "IX_Item_ExpenseAccountId",
                table: "Item");

            migrationBuilder.DropIndex(
                name: "IX_Item_IncomeAccountId",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "ExpenseAccountId",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "IncomeAccountId",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AssetCategory");

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultPayableAccountId1",
                table: "Supplier",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Supplier_DefaultPayableAccountId1",
                table: "Supplier",
                column: "DefaultPayableAccountId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Supplier_Account_DefaultPayableAccountId1",
                table: "Supplier",
                column: "DefaultPayableAccountId1",
                principalTable: "Account",
                principalColumn: "Id");
        }
    }
}
