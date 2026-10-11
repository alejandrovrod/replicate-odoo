using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesInvoiceReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReturn",
                table: "SalesInvoice",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ReturnAgainstId",
                table: "SalesInvoice",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoice_ReturnAgainstId",
                table: "SalesInvoice",
                column: "ReturnAgainstId");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoice_ReturnAgainst",
                table: "SalesInvoice",
                column: "ReturnAgainstId",
                principalTable: "SalesInvoice",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoice_ReturnAgainst",
                table: "SalesInvoice");

            migrationBuilder.DropIndex(
                name: "IX_SalesInvoice_ReturnAgainstId",
                table: "SalesInvoice");

            migrationBuilder.DropColumn(
                name: "IsReturn",
                table: "SalesInvoice");

            migrationBuilder.DropColumn(
                name: "ReturnAgainstId",
                table: "SalesInvoice");
        }
    }
}
