using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountTypeAndUniqueCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Account_Tenant_Company_Code",
                table: "Account");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Account",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Other");

            // ------------------------------------------------------------------------------
            // Backfill (Task 1.1): the ADD above leaves every pre-existing row at the SQL
            // DEFAULT 'Other' (16 seeded rows - scripts/seed-dev-coa.sql, seed-dev-stock.sql and
            // seed-dev-buying.sql). Upgrade them to a sensible ERPNext account_type BEFORE the
            // unique index is created:
            //
            //   Code-specific overrides (identified by AccountCode):
            //     1110 Cash and Cash Equivalents        -> Cash
            //     1120 Accounts Receivable              -> Receivable
            //     1130 Input Tax Recoverable            -> Tax
            //     1310 Stock In Hand                    -> Stock
            //     2110 Accounts Payable                 -> Payable
            //     2120 Stock Received But Not Billed    -> Other (interim liability: ERPNext
            //                                              leaves account_type blank here)
            //     4110 Sales Revenue                    -> Revenue
            //     5210 Cost of Goods Sold               -> COGS
            //     6100 Depreciation Expense             -> Depreciation
            //
            //   Everything else (group roots and unlisted children) uses the per-RootType
            //   default, identical to CreateAccountCommandHandler.DefaultTypeFor:
            //     Equity -> Equity, Income -> Revenue, Expense -> Expense,
            //     Asset / Liability (roots 1000 / 2000) -> Other
            //
            //   Only rows still at the untouched default are updated (idempotent if re-applied
            //   by hand); 5110 Office Supplies and 5120 Purchase Price Difference land on
            //   Expense through the RootType branch.
            // ------------------------------------------------------------------------------
            migrationBuilder.Sql(
                "UPDATE dbo.Account "
                + "SET [Type] = CASE AccountCode "
                + "    WHEN N'1110' THEN N'Cash' "
                + "    WHEN N'1120' THEN N'Receivable' "
                + "    WHEN N'1130' THEN N'Tax' "
                + "    WHEN N'1310' THEN N'Stock' "
                + "    WHEN N'2110' THEN N'Payable' "
                + "    WHEN N'2120' THEN N'Other' "
                + "    WHEN N'4110' THEN N'Revenue' "
                + "    WHEN N'5210' THEN N'COGS' "
                + "    WHEN N'6100' THEN N'Depreciation' "
                + "    ELSE CASE RootType "
                + "        WHEN N'Equity'  THEN N'Equity' "
                + "        WHEN N'Income'  THEN N'Revenue' "
                + "        WHEN N'Expense' THEN N'Expense' "
                + "        ELSE N'Other' "
                + "    END "
                + "END "
                + "WHERE [Type] = N'Other';");

            migrationBuilder.CreateIndex(
                name: "UQ_Account_Tenant_Company_Code",
                table: "Account",
                columns: new[] { "TenantId", "CompanyId", "AccountCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Account_Tenant_Company_Code",
                table: "Account");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Account");

            migrationBuilder.CreateIndex(
                name: "IX_Account_Tenant_Company_Code",
                table: "Account",
                columns: new[] { "TenantId", "CompanyId", "AccountCode" });
        }
    }
}
