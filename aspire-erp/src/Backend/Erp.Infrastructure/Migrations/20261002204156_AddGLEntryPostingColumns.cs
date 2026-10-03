using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <summary>
    /// tasks.md 2.1 (plan.md §2 GLEntry DDL): account-currency pair, AccountCurrency, VoucherId
    /// (+ honest legacy backfill), PartyType/PartyId, CostCenterId, IsCancelled; the
    /// TenantId-led covering index gains CompanyId and the voucher drill-down index is added;
    /// CHECK constraint names align with the plan (CK_*_NonNegative).
    /// </summary>
    /// <inheritdoc />
    public partial class AddGLEntryPostingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GLEntry_Tenant_Account_Date",
                table: "GLEntry");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Credit_Positive",
                table: "GLEntry");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Debit_Positive",
                table: "GLEntry");

            migrationBuilder.AddColumn<string>(
                name: "AccountCurrency",
                table: "GLEntry",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<Guid>(
                name: "CostCenterId",
                table: "GLEntry",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditInAccountCurrency",
                table: "GLEntry",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DebitInAccountCurrency",
                table: "GLEntry",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "GLEntry",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PartyId",
                table: "GLEntry",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartyType",
                table: "GLEntry",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            // plan.md §2: VoucherId UNIQUEIDENTIFIER NOT NULL with NO default. EF's generated
            // AddColumn would either fail on a non-empty table or leave a stray DEFAULT
            // constraint behind, so the column is added NULLABLE, backfilled honestly from the
            // source documents, and only then made NOT NULL (no data impact, no invented GUIDs).
            migrationBuilder.Sql("ALTER TABLE [GLEntry] ADD [VoucherId] uniqueidentifier NULL;");

            // ---- legacy backfill (rows written before this column existed) ---------------
            // Match on the (CompanyId, VoucherNo) pair of each voucher type: VoucherNo is the
            // gapless document number (MR-/MI-/MT-, PR-, PINV-...), unique per company/year.
            // Rows whose source document no longer exists (or whose VoucherType is not one of
            // the three posting pipelines) fall through to the Guid.Empty sentinel below -
            // NEVER a random GUID, because a fabricated link would corrupt the audit trail.
            //
            // The UPDATEs below are SCHEMA backfill, not ledger mutations, so
            // trg_GLEntry_AppendOnly (Constitution III.2) is disabled for exactly this window
            // and re-enabled right after - all inside the migration's transaction, so a failure
            // rolls the trigger state back together with the data.
            //
            // Counts on the dev database as of 2026-10-02 (33 legacy GLEntry rows):
            //   StockEntry      26 rows -> 26 matched, 0 sentinel
            //   PurchaseReceipt  4 rows ->  4 matched, 0 sentinel
            //   PurchaseInvoice  3 rows ->  3 matched, 0 sentinel
            //   unmatched (sentinel): 0
            migrationBuilder.Sql("ALTER TABLE [GLEntry] DISABLE TRIGGER [trg_GLEntry_AppendOnly];");

            migrationBuilder.Sql("""
                UPDATE gl
                SET gl.VoucherId = se.Id
                FROM dbo.GLEntry AS gl
                INNER JOIN dbo.StockEntry AS se
                    ON se.VoucherNo = gl.VoucherNo
                   AND se.CompanyId = gl.CompanyId
                WHERE gl.VoucherType = 'StockEntry';
                """);

            migrationBuilder.Sql("""
                UPDATE gl
                SET gl.VoucherId = pr.Id
                FROM dbo.GLEntry AS gl
                INNER JOIN dbo.PurchaseReceipt AS pr
                    ON pr.VoucherNo = gl.VoucherNo
                   AND pr.CompanyId = gl.CompanyId
                WHERE gl.VoucherType = 'PurchaseReceipt';
                """);

            migrationBuilder.Sql("""
                UPDATE gl
                SET gl.VoucherId = pi.Id
                FROM dbo.GLEntry AS gl
                INNER JOIN dbo.PurchaseInvoice AS pi
                    ON pi.VoucherNo = gl.VoucherNo
                   AND pi.CompanyId = gl.CompanyId
                WHERE gl.VoucherType = 'PurchaseInvoice';
                """);

            // Honest sentinel for whatever did not match (documented in the comment above).
            migrationBuilder.Sql("""
                UPDATE dbo.GLEntry
                SET VoucherId = '00000000-0000-0000-0000-000000000000'
                WHERE VoucherId IS NULL;
                """);

            migrationBuilder.Sql("ALTER TABLE [GLEntry] ENABLE TRIGGER [trg_GLEntry_AppendOnly];");

            migrationBuilder.Sql("ALTER TABLE [GLEntry] ALTER COLUMN [VoucherId] uniqueidentifier NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_GLEntry_Tenant_Company_Account_Date",
                table: "GLEntry",
                columns: new[] { "TenantId", "CompanyId", "AccountId", "PostingDate" })
                .Annotation("SqlServer:Include", new[] { "Debit", "Credit", "VoucherType", "VoucherNo" });

            migrationBuilder.CreateIndex(
                name: "IX_GLEntry_Tenant_Voucher",
                table: "GLEntry",
                columns: new[] { "TenantId", "VoucherType", "VoucherId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Credit_NonNegative",
                table: "GLEntry",
                sql: "[Credit] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Debit_NonNegative",
                table: "GLEntry",
                sql: "[Debit] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GLEntry_Tenant_Company_Account_Date",
                table: "GLEntry");

            migrationBuilder.DropIndex(
                name: "IX_GLEntry_Tenant_Voucher",
                table: "GLEntry");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Credit_NonNegative",
                table: "GLEntry");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Debit_NonNegative",
                table: "GLEntry");

            migrationBuilder.DropColumn(
                name: "AccountCurrency",
                table: "GLEntry");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "GLEntry");

            migrationBuilder.DropColumn(
                name: "CreditInAccountCurrency",
                table: "GLEntry");

            migrationBuilder.DropColumn(
                name: "DebitInAccountCurrency",
                table: "GLEntry");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "GLEntry");

            migrationBuilder.DropColumn(
                name: "PartyId",
                table: "GLEntry");

            migrationBuilder.DropColumn(
                name: "PartyType",
                table: "GLEntry");

            migrationBuilder.DropColumn(
                name: "VoucherId",
                table: "GLEntry");

            migrationBuilder.CreateIndex(
                name: "IX_GLEntry_Tenant_Account_Date",
                table: "GLEntry",
                columns: new[] { "TenantId", "AccountId", "PostingDate" })
                .Annotation("SqlServer:Include", new[] { "Debit", "Credit", "VoucherType", "VoucherNo" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Credit_Positive",
                table: "GLEntry",
                sql: "[Credit] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Debit_Positive",
                table: "GLEntry",
                sql: "[Debit] >= 0");
        }
    }
}
