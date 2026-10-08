using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixFxCascadePaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExchangeRateRevaluationLines_Account_AccountId",
                table: "ExchangeRateRevaluationLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ExchangeRateRevaluationLines_Currency_CurrencyId",
                table: "ExchangeRateRevaluationLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ExchangeRateRevaluations_Account_ExchangeGainLossAccountId",
                table: "ExchangeRateRevaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_ExchangeRateRevaluations_Company_CompanyId",
                table: "ExchangeRateRevaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_ExchangeRates_Currency_FromCurrencyId",
                table: "ExchangeRates");

            migrationBuilder.DropForeignKey(
                name: "FK_ExchangeRates_Currency_ToCurrencyId",
                table: "ExchangeRates");

            migrationBuilder.AlterColumn<decimal>(
                name: "Rate",
                table: "ExchangeRates",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "RoundingLossAllowance",
                table: "ExchangeRateRevaluations",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "NewExchangeRate",
                table: "ExchangeRateRevaluationLines",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "GainLossAmount",
                table: "ExchangeRateRevaluationLines",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentExchangeRate",
                table: "ExchangeRateRevaluationLines",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "BalanceInForeignCurrency",
                table: "ExchangeRateRevaluationLines",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "BalanceInBaseCurrency",
                table: "ExchangeRateRevaluationLines",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddForeignKey(
                name: "FK_ExchangeRateRevaluationLines_Account_AccountId",
                table: "ExchangeRateRevaluationLines",
                column: "AccountId",
                principalTable: "Account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExchangeRateRevaluationLines_Currency_CurrencyId",
                table: "ExchangeRateRevaluationLines",
                column: "CurrencyId",
                principalTable: "Currency",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExchangeRateRevaluations_Account_ExchangeGainLossAccountId",
                table: "ExchangeRateRevaluations",
                column: "ExchangeGainLossAccountId",
                principalTable: "Account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExchangeRateRevaluations_Company_CompanyId",
                table: "ExchangeRateRevaluations",
                column: "CompanyId",
                principalTable: "Company",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExchangeRates_Currency_FromCurrencyId",
                table: "ExchangeRates",
                column: "FromCurrencyId",
                principalTable: "Currency",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExchangeRates_Currency_ToCurrencyId",
                table: "ExchangeRates",
                column: "ToCurrencyId",
                principalTable: "Currency",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExchangeRateRevaluationLines_Account_AccountId",
                table: "ExchangeRateRevaluationLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ExchangeRateRevaluationLines_Currency_CurrencyId",
                table: "ExchangeRateRevaluationLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ExchangeRateRevaluations_Account_ExchangeGainLossAccountId",
                table: "ExchangeRateRevaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_ExchangeRateRevaluations_Company_CompanyId",
                table: "ExchangeRateRevaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_ExchangeRates_Currency_FromCurrencyId",
                table: "ExchangeRates");

            migrationBuilder.DropForeignKey(
                name: "FK_ExchangeRates_Currency_ToCurrencyId",
                table: "ExchangeRates");

            migrationBuilder.AlterColumn<decimal>(
                name: "Rate",
                table: "ExchangeRates",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "RoundingLossAllowance",
                table: "ExchangeRateRevaluations",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "NewExchangeRate",
                table: "ExchangeRateRevaluationLines",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "GainLossAmount",
                table: "ExchangeRateRevaluationLines",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentExchangeRate",
                table: "ExchangeRateRevaluationLines",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "BalanceInForeignCurrency",
                table: "ExchangeRateRevaluationLines",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "BalanceInBaseCurrency",
                table: "ExchangeRateRevaluationLines",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AddForeignKey(
                name: "FK_ExchangeRateRevaluationLines_Account_AccountId",
                table: "ExchangeRateRevaluationLines",
                column: "AccountId",
                principalTable: "Account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ExchangeRateRevaluationLines_Currency_CurrencyId",
                table: "ExchangeRateRevaluationLines",
                column: "CurrencyId",
                principalTable: "Currency",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ExchangeRateRevaluations_Account_ExchangeGainLossAccountId",
                table: "ExchangeRateRevaluations",
                column: "ExchangeGainLossAccountId",
                principalTable: "Account",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ExchangeRateRevaluations_Company_CompanyId",
                table: "ExchangeRateRevaluations",
                column: "CompanyId",
                principalTable: "Company",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ExchangeRates_Currency_FromCurrencyId",
                table: "ExchangeRates",
                column: "FromCurrencyId",
                principalTable: "Currency",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ExchangeRates_Currency_ToCurrencyId",
                table: "ExchangeRates",
                column: "ToCurrencyId",
                principalTable: "Currency",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
