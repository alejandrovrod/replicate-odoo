using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierDefaultPayableAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
