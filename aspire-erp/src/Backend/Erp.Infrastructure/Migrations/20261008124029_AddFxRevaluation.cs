using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFxRevaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyId",
                table: "SalesInvoice",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "SalesInvoice",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyId",
                table: "PurchaseInvoice",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "PurchaseInvoice",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SettlementExchangeRate",
                table: "PaymentEntry",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "TransactionCurrencyId",
                table: "PaymentEntry",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultExchangeGainLossAccountCode",
                table: "Company",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultExchangeGainLossAccountId",
                table: "Company",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExchangeRateRevaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VoucherNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PostingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExchangeGainLossAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoundingLossAllowance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DocumentStatus = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRateRevaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExchangeRateRevaluations_Account_ExchangeGainLossAccountId",
                        column: x => x.ExchangeGainLossAccountId,
                        principalTable: "Account",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExchangeRateRevaluations_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromCurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToCurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RateDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExchangeRates_Currency_FromCurrencyId",
                        column: x => x.FromCurrencyId,
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExchangeRates_Currency_ToCurrencyId",
                        column: x => x.ToCurrencyId,
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRateRevaluationLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExchangeRateRevaluationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BalanceInForeignCurrency = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceInBaseCurrency = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentExchangeRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewExchangeRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GainLossAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRateRevaluationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExchangeRateRevaluationLines_Account_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExchangeRateRevaluationLines_Currency_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExchangeRateRevaluationLines_ExchangeRateRevaluations_ExchangeRateRevaluationId",
                        column: x => x.ExchangeRateRevaluationId,
                        principalTable: "ExchangeRateRevaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoice_CurrencyId",
                table: "SalesInvoice",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoice_CurrencyId",
                table: "PurchaseInvoice",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEntry_TransactionCurrencyId",
                table: "PaymentEntry",
                column: "TransactionCurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRateRevaluationLines_AccountId",
                table: "ExchangeRateRevaluationLines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRateRevaluationLines_CurrencyId",
                table: "ExchangeRateRevaluationLines",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRateRevaluationLines_ExchangeRateRevaluationId",
                table: "ExchangeRateRevaluationLines",
                column: "ExchangeRateRevaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRateRevaluations_CompanyId",
                table: "ExchangeRateRevaluations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRateRevaluations_ExchangeGainLossAccountId",
                table: "ExchangeRateRevaluations",
                column: "ExchangeGainLossAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_FromCurrencyId",
                table: "ExchangeRates",
                column: "FromCurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_ToCurrencyId",
                table: "ExchangeRates",
                column: "ToCurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentEntry_Currency_TransactionCurrencyId",
                table: "PaymentEntry",
                column: "TransactionCurrencyId",
                principalTable: "Currency",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoice_Currency_CurrencyId",
                table: "PurchaseInvoice",
                column: "CurrencyId",
                principalTable: "Currency",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoice_Currency_CurrencyId",
                table: "SalesInvoice",
                column: "CurrencyId",
                principalTable: "Currency",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentEntry_Currency_TransactionCurrencyId",
                table: "PaymentEntry");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoice_Currency_CurrencyId",
                table: "PurchaseInvoice");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoice_Currency_CurrencyId",
                table: "SalesInvoice");

            migrationBuilder.DropTable(
                name: "ExchangeRateRevaluationLines");

            migrationBuilder.DropTable(
                name: "ExchangeRates");

            migrationBuilder.DropTable(
                name: "ExchangeRateRevaluations");

            migrationBuilder.DropIndex(
                name: "IX_SalesInvoice_CurrencyId",
                table: "SalesInvoice");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoice_CurrencyId",
                table: "PurchaseInvoice");

            migrationBuilder.DropIndex(
                name: "IX_PaymentEntry_TransactionCurrencyId",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "SalesInvoice");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "SalesInvoice");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "PurchaseInvoice");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "PurchaseInvoice");

            migrationBuilder.DropColumn(
                name: "SettlementExchangeRate",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "TransactionCurrencyId",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "DefaultExchangeGainLossAccountCode",
                table: "Company");

            migrationBuilder.DropColumn(
                name: "DefaultExchangeGainLossAccountId",
                table: "Company");
        }
    }
}
