using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CurrencyCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingCurrency",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Opportunity");

            migrationBuilder.DropColumn(
                name: "BillingCurrency",
                table: "Customer");

            migrationBuilder.DropColumn(
                name: "DefaultCurrency",
                table: "Company");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "BankAccount");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Account");

            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyId",
                table: "Supplier",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyId",
                table: "Opportunity",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyId",
                table: "Customer",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyId",
                table: "Company",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyId",
                table: "BankAccount",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyId",
                table: "Account",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Currency",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FractionName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currency", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Supplier_CurrencyId",
                table: "Supplier",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunity_CurrencyId",
                table: "Opportunity",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Customer_CurrencyId",
                table: "Customer",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Company_CurrencyId",
                table: "Company",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccount_CurrencyId",
                table: "BankAccount",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Account_CurrencyId",
                table: "Account",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "UQ_Currency_Code",
                table: "Currency",
                column: "Code",
                unique: true);

            // RM-09 seed: the reference ISO catalog (global, shared by all tenants).
            migrationBuilder.InsertData(
                table: "Currency",
                columns: new[] { "Id", "Code", "Symbol", "FractionName", "IsActive" },
                values: new object[,]
                {
                    { new Guid("c0000000-0000-4000-8000-000000000001"), "USD", "$", "Cent", true },
                    { new Guid("c0000000-0000-4000-8000-000000000002"), "EUR", "€", "Cent", true },
                    { new Guid("c0000000-0000-4000-8000-000000000003"), "GBP", "£", "Penny", true },
                    { new Guid("c0000000-0000-4000-8000-000000000004"), "ARS", "$", "Centavo", true },
                    { new Guid("c0000000-0000-4000-8000-000000000005"), "BRL", "R$", "Centavo", true },
                    { new Guid("c0000000-0000-4000-8000-000000000006"), "MXN", "$", "Centavo", true },
                    { new Guid("c0000000-0000-4000-8000-000000000007"), "CLP", "$", "Peso", true },
                    { new Guid("c0000000-0000-4000-8000-000000000008"), "COP", "$", "Centavo", true },
                    { new Guid("c0000000-0000-4000-8000-000000000009"), "PEN", "S/", "Céntimo", true },
                    { new Guid("c0000000-0000-4000-8000-000000000010"), "UYU", "$", "Centésimo", true },
                    { new Guid("c0000000-0000-4000-8000-000000000011"), "PYG", "₲", "Céntimo", true },
                    { new Guid("c0000000-0000-4000-8000-000000000012"), "BOB", "Bs", "Centavo", true },
                });

            migrationBuilder.AddForeignKey(
                name: "FK_Account_Currency",
                table: "Account",
                column: "CurrencyId",
                principalTable: "Currency",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BankAccount_Currency",
                table: "BankAccount",
                column: "CurrencyId",
                principalTable: "Currency",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Company_Currency",
                table: "Company",
                column: "CurrencyId",
                principalTable: "Currency",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Customer_Currency",
                table: "Customer",
                column: "CurrencyId",
                principalTable: "Currency",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Opportunity_Currency",
                table: "Opportunity",
                column: "CurrencyId",
                principalTable: "Currency",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Supplier_Currency",
                table: "Supplier",
                column: "CurrencyId",
                principalTable: "Currency",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Currency",
                keyColumn: "Code",
                keyValues: new object[] { "USD", "EUR", "GBP", "ARS", "BRL", "MXN", "CLP", "COP", "PEN", "UYU", "PYG", "BOB" });

            migrationBuilder.DropForeignKey(
                name: "FK_Account_Currency",
                table: "Account");

            migrationBuilder.DropForeignKey(
                name: "FK_BankAccount_Currency",
                table: "BankAccount");

            migrationBuilder.DropForeignKey(
                name: "FK_Company_Currency",
                table: "Company");

            migrationBuilder.DropForeignKey(
                name: "FK_Customer_Currency",
                table: "Customer");

            migrationBuilder.DropForeignKey(
                name: "FK_Opportunity_Currency",
                table: "Opportunity");

            migrationBuilder.DropForeignKey(
                name: "FK_Supplier_Currency",
                table: "Supplier");

            migrationBuilder.DropTable(
                name: "Currency");

            migrationBuilder.DropIndex(
                name: "IX_Supplier_CurrencyId",
                table: "Supplier");

            migrationBuilder.DropIndex(
                name: "IX_Opportunity_CurrencyId",
                table: "Opportunity");

            migrationBuilder.DropIndex(
                name: "IX_Customer_CurrencyId",
                table: "Customer");

            migrationBuilder.DropIndex(
                name: "IX_Company_CurrencyId",
                table: "Company");

            migrationBuilder.DropIndex(
                name: "IX_BankAccount_CurrencyId",
                table: "BankAccount");

            migrationBuilder.DropIndex(
                name: "IX_Account_CurrencyId",
                table: "Account");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "Opportunity");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "Customer");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "Company");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "BankAccount");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "Account");

            migrationBuilder.AddColumn<string>(
                name: "BillingCurrency",
                table: "Supplier",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Opportunity",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "BillingCurrency",
                table: "Customer",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "DefaultCurrency",
                table: "Company",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "BankAccount",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Account",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");
        }
    }
}
