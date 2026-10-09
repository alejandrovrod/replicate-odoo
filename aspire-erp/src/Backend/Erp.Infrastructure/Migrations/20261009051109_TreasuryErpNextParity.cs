using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TreasuryErpNextParity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentEntry_Direction",
                table: "PaymentEntry");

            migrationBuilder.AddColumn<decimal>(
                name: "BasePaidAmount",
                table: "PaymentEntry",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseReceivedAmount",
                table: "PaymentEntry",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "CostCenterId",
                table: "PaymentEntry",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DifferenceAmount",
                table: "PaymentEntry",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ModeOfPayment",
                table: "PaymentEntry",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PaidFromAccountCurrency",
                table: "PaymentEntry",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<Guid>(
                name: "PaidFromAccountId",
                table: "PaymentEntry",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaidToAccountCurrency",
                table: "PaymentEntry",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<Guid>(
                name: "PaidToAccountId",
                table: "PaymentEntry",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartyName",
                table: "PaymentEntry",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "PaymentEntry",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedAmount",
                table: "PaymentEntry",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReferenceDate",
                table: "PaymentEntry",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "PaymentEntry",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "SourceExchangeRate",
                table: "PaymentEntry",
                type: "decimal(18,6)",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetExchangeRate",
                table: "PaymentEntry",
                type: "decimal(18,6)",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAllocatedAmount",
                table: "PaymentEntry",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "PaymentAllocation",
                type: "decimal(18,6)",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "OutstandingAmount",
                table: "PaymentAllocation",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferenceDocumentId",
                table: "PaymentAllocation",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceDocumentType",
                table: "PaymentAllocation",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "PaymentAllocation",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "BankPartyAccountNumber",
                table: "BankTransaction",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankPartyIban",
                table: "BankTransaction",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankPartyName",
                table: "BankTransaction",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ExcludedFee",
                table: "BankTransaction",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IncludedFee",
                table: "BankTransaction",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsRuleEvaluated",
                table: "BankTransaction",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "MatchedTransactionRuleId",
                table: "BankTransaction",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PartyId",
                table: "BankTransaction",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartyType",
                table: "BankTransaction",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionType",
                table: "BankTransaction",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "UnallocatedAmount",
                table: "BankTransaction",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentEntry_Direction",
                table: "PaymentEntry",
                sql: "([PaymentType] = 'Receive' AND [PartyType] = 'Customer') OR ([PaymentType] = 'Pay' AND [PartyType] IN ('Supplier', 'Employee')) OR ([PaymentType] = 'InternalTransfer')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentEntry_Direction",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "BasePaidAmount",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "BaseReceivedAmount",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "DifferenceAmount",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "ModeOfPayment",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "PaidFromAccountCurrency",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "PaidFromAccountId",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "PaidToAccountCurrency",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "PaidToAccountId",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "PartyName",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "ReceivedAmount",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "ReferenceDate",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "SourceExchangeRate",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "TargetExchangeRate",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "TotalAllocatedAmount",
                table: "PaymentEntry");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "PaymentAllocation");

            migrationBuilder.DropColumn(
                name: "OutstandingAmount",
                table: "PaymentAllocation");

            migrationBuilder.DropColumn(
                name: "ReferenceDocumentId",
                table: "PaymentAllocation");

            migrationBuilder.DropColumn(
                name: "ReferenceDocumentType",
                table: "PaymentAllocation");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "PaymentAllocation");

            migrationBuilder.DropColumn(
                name: "BankPartyAccountNumber",
                table: "BankTransaction");

            migrationBuilder.DropColumn(
                name: "BankPartyIban",
                table: "BankTransaction");

            migrationBuilder.DropColumn(
                name: "BankPartyName",
                table: "BankTransaction");

            migrationBuilder.DropColumn(
                name: "ExcludedFee",
                table: "BankTransaction");

            migrationBuilder.DropColumn(
                name: "IncludedFee",
                table: "BankTransaction");

            migrationBuilder.DropColumn(
                name: "IsRuleEvaluated",
                table: "BankTransaction");

            migrationBuilder.DropColumn(
                name: "MatchedTransactionRuleId",
                table: "BankTransaction");

            migrationBuilder.DropColumn(
                name: "PartyId",
                table: "BankTransaction");

            migrationBuilder.DropColumn(
                name: "PartyType",
                table: "BankTransaction");

            migrationBuilder.DropColumn(
                name: "TransactionType",
                table: "BankTransaction");

            migrationBuilder.DropColumn(
                name: "UnallocatedAmount",
                table: "BankTransaction");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentEntry_Direction",
                table: "PaymentEntry",
                sql: "([PaymentType] = 'Receive' AND [PartyType] = 'Customer') OR ([PaymentType] = 'Pay' AND [PartyType] = 'Supplier')");
        }
    }
}
