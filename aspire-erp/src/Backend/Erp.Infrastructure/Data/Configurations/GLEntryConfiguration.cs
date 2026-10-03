using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// plan.md §2 GLEntry table (verbatim): BIGINT IDENTITY key, positive-decimal CHECK
/// constraints (Constitution IV.3) as <c>CK_Debit_NonNegative</c> / <c>CK_Credit_NonNegative</c>,
/// the account-currency and voucher provenance columns, and the two covering indexes
/// <c>IX_GLEntry_Tenant_Company_Account_Date INCLUDE (Debit, Credit, VoucherType, VoucherNo)</c>
/// and <c>IX_GLEntry_Tenant_Voucher</c> (Constitution IV.1: TenantId leads every index). The
/// INSTEAD OF UPDATE/DELETE trigger is added by the migration itself (Constitution III.2,
/// database level).
/// </summary>
public sealed class GLEntryConfiguration : IEntityTypeConfiguration<GLEntry>
{
    public void Configure(EntityTypeBuilder<GLEntry> builder)
    {
        builder.ToTable("GLEntry", table =>
        {
            // plan.md §2 / Constitution IV.3 (names aligned with the plan's DDL: the initial
            // migration shipped CK_Debit_Positive/CK_Credit_Positive - renamed by
            // AddGLEntryPostingColumns, same predicate, no data impact).
            table.HasCheckConstraint("CK_Debit_NonNegative", "[Debit] >= 0");
            table.HasCheckConstraint("CK_Credit_NonNegative", "[Credit] >= 0");
        });

        // BIGINT IDENTITY(1,1) per plan §2 (numeric key = value generated on add by convention;
        // stated explicitly so the intent survives any convention change).
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id)
            .ValueGeneratedOnAdd();

        builder.Property(g => g.PostingDate).HasColumnType("date").IsRequired();
        builder.Property(g => g.Debit).HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(g => g.Credit).HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();

        // plan.md §2 account-currency pair + snapshot currency (single-currency postings book 1:1).
        builder.Property(g => g.DebitInAccountCurrency)
            .HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(g => g.CreditInAccountCurrency)
            .HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(g => g.AccountCurrency)
            .HasMaxLength(3).IsRequired().HasDefaultValue("USD");

        builder.Property(g => g.VoucherType).HasMaxLength(50).IsRequired();
        builder.Property(g => g.VoucherNo).HasMaxLength(100).IsRequired();

        // plan.md §2: VoucherId is NOT NULL with no default; the migration backfills legacy rows
        // (sentinel Guid.Empty when the source document cannot be matched) and only then enforces
        // NOT NULL - a plain AddColumn would fail on a non-empty table.
        builder.Property(g => g.VoucherId).IsRequired();

        builder.Property(g => g.PartyType).HasMaxLength(50);
        builder.Property(g => g.IsCancelled).HasColumnType("bit").HasDefaultValue(false).IsRequired();
        builder.Property(g => g.Remarks);
        builder.Property(g => g.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // plan.md §2 FK_GLEntry_Account (plain FK = NO ACTION, EF convention would cascade).
        builder.HasOne(g => g.Account)
            .WithMany()
            .HasForeignKey(g => g.AccountId)
            .HasConstraintName("FK_GLEntry_Account")
            .OnDelete(DeleteBehavior.Restrict);

        // Constitution IV.1 + plan §2: TenantId leads, CompanyId joins the key so company-scoped
        // ledger reads (trial balance, GL by account) stay covered, with the plan's INCLUDE columns.
        builder.HasIndex(g => new { g.TenantId, g.CompanyId, g.AccountId, g.PostingDate })
            .HasDatabaseName("IX_GLEntry_Tenant_Company_Account_Date")
            .IncludeProperties(g => new { g.Debit, g.Credit, g.VoucherType, g.VoucherNo });

        // plan §2: voucher drill-down (spec AC-07: find every line of a document to reverse it).
        builder.HasIndex(g => new { g.TenantId, g.VoucherType, g.VoucherId })
            .HasDatabaseName("IX_GLEntry_Tenant_Voucher");
    }
}
