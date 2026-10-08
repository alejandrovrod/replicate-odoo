using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalYearAndHardenPeriodClosing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_PeriodClosingVouchers_CompanyId_VoucherNo",
                table: "PeriodClosingVouchers",
                newName: "UQ_PCV_Company_VoucherNo");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "PeriodClosingVouchers",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "PeriodClosingVouchers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWSEQUENTIALID()",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "FiscalYearId",
                table: "PeriodClosingVouchers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "PeriodClosingVouchers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FiscalYears",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    YearName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalYears", x => x.Id);
                    table.CheckConstraint("CK_FY_ClosedAt", "(([IsClosed] = 0 AND [ClosedAt] IS NULL) OR ([IsClosed] = 1))");
                    table.CheckConstraint("CK_FY_Dates", "[StartDate] < [EndDate]");
                    table.ForeignKey(
                        name: "FK_FY_Company",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PeriodClosingVoucherLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VoucherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    Credit = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodClosingVoucherLines", x => x.Id);
                    table.CheckConstraint("CK_PCVL_NonNeg", "[Debit] >= 0 AND [Credit] >= 0 AND ([Debit] > 0 OR [Credit] > 0)");
                    table.ForeignKey(
                        name: "FK_PCVL_Account",
                        column: x => x.AccountId,
                        principalTable: "Account",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PCVL_Voucher",
                        column: x => x.VoucherId,
                        principalTable: "PeriodClosingVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PCV_Tenant_Company_Status_Date",
                table: "PeriodClosingVouchers",
                columns: new[] { "TenantId", "CompanyId", "DocumentStatus", "PostingDate" })
                .Annotation("SqlServer:Include", new[] { "FiscalYearId", "VoucherNo" });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodClosingVouchers_FiscalYearId",
                table: "PeriodClosingVouchers",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodClosingVouchers_RetainedEarningsAccountId",
                table: "PeriodClosingVouchers",
                column: "RetainedEarningsAccountId");

            migrationBuilder.CreateIndex(
                name: "UQ_One_Submitted_Close_Per_Year",
                table: "PeriodClosingVouchers",
                columns: new[] { "CompanyId", "FiscalYearId" },
                unique: true,
                filter: "[DocumentStatus] = 'Submitted'");

            migrationBuilder.CreateIndex(
                name: "UQ_PCV_Idempotency",
                table: "PeriodClosingVouchers",
                columns: new[] { "TenantId", "CompanyId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PCV_Status",
                table: "PeriodClosingVouchers",
                sql: "[DocumentStatus] IN ('Draft','Submitted','Cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_FY_Tenant_Company_Closed",
                table: "FiscalYears",
                columns: new[] { "TenantId", "CompanyId", "IsClosed" })
                .Annotation("SqlServer:Include", new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "UQ_FY_Company_Year",
                table: "FiscalYears",
                columns: new[] { "CompanyId", "YearName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PCVL_Voucher",
                table: "PeriodClosingVoucherLines",
                column: "VoucherId")
                .Annotation("SqlServer:Include", new[] { "AccountId", "Debit", "Credit" });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodClosingVoucherLines_AccountId",
                table: "PeriodClosingVoucherLines",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_PCV_Company",
                table: "PeriodClosingVouchers",
                column: "CompanyId",
                principalTable: "Company",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PCV_FiscalYear",
                table: "PeriodClosingVouchers",
                column: "FiscalYearId",
                principalTable: "FiscalYears",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PCV_Retained",
                table: "PeriodClosingVouchers",
                column: "RetainedEarningsAccountId",
                principalTable: "Account",
                principalColumn: "Id");

            // Task 2.5 (spec FC-03 enabler): without a 3100 Equity leaf per company, submit can
            // never satisfy the retained-earnings gate. Seed one active non-group Equity leaf
            // '3100 - Retained Earnings' for every company lacking it and backfill the company
            // default where null. Idempotent: guarded by NOT EXISTS / IS NULL.
            migrationBuilder.Sql(
                """
                INSERT INTO [Account] (TenantId, CompanyId, AccountCode, AccountName, RootType, IsGroup, IsActive)
                SELECT c.TenantId, c.Id, '3100', 'Retained Earnings', 'Equity', 0, 1
                FROM [Company] c
                WHERE NOT EXISTS (
                    SELECT 1 FROM [Account] a
                    WHERE a.CompanyId = c.Id AND a.AccountCode = '3100');
                """);

            migrationBuilder.Sql(
                """
                UPDATE c
                SET DefaultRetainedEarningsAccountId = a.Id,
                    DefaultRetainedEarningsAccountCode = '3100'
                FROM [Company] c
                INNER JOIN [Account] a ON a.CompanyId = c.Id AND a.AccountCode = '3100'
                WHERE c.DefaultRetainedEarningsAccountId IS NULL
                  AND a.RootType = 'Equity' AND a.IsGroup = 0 AND a.IsActive = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort rollback of the Task 2.5 seed: only removes 3100 leaves this migration
            // could have created (exact name match, no ledger rows referencing them).
            migrationBuilder.Sql(
                """
                UPDATE [Company]
                SET DefaultRetainedEarningsAccountId = NULL,
                    DefaultRetainedEarningsAccountCode = NULL
                WHERE DefaultRetainedEarningsAccountCode = '3100';
                DELETE a
                FROM [Account] a
                WHERE a.AccountCode = '3100' AND a.AccountName = 'Retained Earnings'
                  AND NOT EXISTS (SELECT 1 FROM [GLEntry] g WHERE g.AccountId = a.Id);
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_PCV_Company",
                table: "PeriodClosingVouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_PCV_FiscalYear",
                table: "PeriodClosingVouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_PCV_Retained",
                table: "PeriodClosingVouchers");

            migrationBuilder.DropTable(
                name: "FiscalYears");

            migrationBuilder.DropTable(
                name: "PeriodClosingVoucherLines");

            migrationBuilder.DropIndex(
                name: "IX_PCV_Tenant_Company_Status_Date",
                table: "PeriodClosingVouchers");

            migrationBuilder.DropIndex(
                name: "IX_PeriodClosingVouchers_FiscalYearId",
                table: "PeriodClosingVouchers");

            migrationBuilder.DropIndex(
                name: "IX_PeriodClosingVouchers_RetainedEarningsAccountId",
                table: "PeriodClosingVouchers");

            migrationBuilder.DropIndex(
                name: "UQ_One_Submitted_Close_Per_Year",
                table: "PeriodClosingVouchers");

            migrationBuilder.DropIndex(
                name: "UQ_PCV_Idempotency",
                table: "PeriodClosingVouchers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PCV_Status",
                table: "PeriodClosingVouchers");

            migrationBuilder.DropColumn(
                name: "FiscalYearId",
                table: "PeriodClosingVouchers");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "PeriodClosingVouchers");

            migrationBuilder.RenameIndex(
                name: "UQ_PCV_Company_VoucherNo",
                table: "PeriodClosingVouchers",
                newName: "IX_PeriodClosingVouchers_CompanyId_VoucherNo");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "PeriodClosingVouchers",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldDefaultValueSql: "SYSDATETIMEOFFSET()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "PeriodClosingVouchers",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "NEWSEQUENTIALID()");
        }
    }
}
