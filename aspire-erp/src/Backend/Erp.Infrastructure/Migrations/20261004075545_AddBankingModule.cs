using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBankingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankAccount",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "USD"),
                    GLAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastReconciledBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    LastReconciledDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccount", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankAccount_Company",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankAccount_GLAccount",
                        column: x => x.GLAccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "BankAccountHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "BankStatementImport",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ImportDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    TotalTransactionsCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ImportedCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    DuplicateCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ImportStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Processed")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementImport", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankStatementImport_BankAccount",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementImport_Company",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankTransaction",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Deposit = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    Withdrawal = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "USD"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TransactionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Unreconciled"),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    ClearanceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SuggestedPartyType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SuggestedPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SuggestedAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransaction", x => x.Id);
                    table.CheckConstraint("CK_Deposit_NonNegative", "[Deposit] >= 0");
                    table.CheckConstraint("CK_Withdrawal_NonNegative", "[Withdrawal] >= 0");
                    table.ForeignKey(
                        name: "FK_BankTransaction_BankAccount",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankTransaction_Company",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankTransactionRule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConditionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Pattern = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TargetPartyType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TargetPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AutoCreateVoucher = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    TargetExpenseAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransactionRule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankTransactionRule_BankAccount",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankTransactionRule_Company",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentEntry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Unreconciled"),
                    ClearanceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEntry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentEntry_BankAccount",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentEntry_Company",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankReconciliation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CounterpartType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CounterpartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankReconciliation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankReconciliation_BankTransaction",
                        column: x => x.BankTransactionId,
                        principalTable: "BankTransaction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAllocation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAllocation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAllocation_PaymentEntry_PaymentEntryId",
                        column: x => x.PaymentEntryId,
                        principalTable: "PaymentEntry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentAllocation_SalesInvoice",
                        column: x => x.SalesInvoiceId,
                        principalTable: "SalesInvoice",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccount_CompanyId",
                table: "BankAccount",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccount_GLAccountId",
                table: "BankAccount",
                column: "GLAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccount_Tenant_Company_Number",
                table: "BankAccount",
                columns: new[] { "TenantId", "CompanyId", "AccountNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliation_BankTransactionId",
                table: "BankReconciliation",
                column: "BankTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliation_Tenant_Counterpart",
                table: "BankReconciliation",
                columns: new[] { "TenantId", "CounterpartType", "CounterpartId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliation_Tenant_Transaction",
                table: "BankReconciliation",
                columns: new[] { "TenantId", "BankTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementImport_BankAccountId",
                table: "BankStatementImport",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementImport_CompanyId",
                table: "BankStatementImport",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementImport_Tenant_Account_Date",
                table: "BankStatementImport",
                columns: new[] { "TenantId", "BankAccountId", "ImportDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransaction_BankAccountId",
                table: "BankTransaction",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransaction_CompanyId",
                table: "BankTransaction",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransaction_Tenant_Account_Status",
                table: "BankTransaction",
                columns: new[] { "TenantId", "BankAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionRule_BankAccountId",
                table: "BankTransactionRule",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionRule_CompanyId",
                table: "BankTransactionRule",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionRule_Tenant_Company_Priority",
                table: "BankTransactionRule",
                columns: new[] { "TenantId", "CompanyId", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocation_PaymentEntryId",
                table: "PaymentAllocation",
                column: "PaymentEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocation_SalesInvoiceId",
                table: "PaymentAllocation",
                column: "SalesInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocation_Tenant_Payment",
                table: "PaymentAllocation",
                columns: new[] { "TenantId", "PaymentEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEntry_BankAccountId",
                table: "PaymentEntry",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEntry_CompanyId",
                table: "PaymentEntry",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEntry_Tenant_Company_Status",
                table: "PaymentEntry",
                columns: new[] { "TenantId", "CompanyId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankReconciliation");

            migrationBuilder.DropTable(
                name: "BankStatementImport");

            migrationBuilder.DropTable(
                name: "BankTransactionRule");

            migrationBuilder.DropTable(
                name: "PaymentAllocation");

            migrationBuilder.DropTable(
                name: "BankTransaction");

            migrationBuilder.DropTable(
                name: "PaymentEntry");

            migrationBuilder.DropTable(
                name: "BankAccount")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "BankAccountHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");
        }
    }
}
