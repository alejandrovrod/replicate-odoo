using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// plan.md §7.2: Company table (multi-company per tenant) with FK to Tenant.
/// </summary>
public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        // Constitution Article IV.2: master entity Company must be system-versioned. plan.md §7's
        // DDL omits this for Company - the Constitution overrides the plan. In EF 10 the temporal
        // configuration hangs off the ToTable(...) builder.
        builder.ToTable("Company", table =>
            table.IsTemporal(t => t.UseHistoryTable("CompanyHistory")));

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
        builder.Property(c => c.CurrencyId);
        builder.Property(c => c.TaxId).HasMaxLength(50).IsRequired();

        // Phase 3 additions (decision D2): the two plan.md §2 columns the initial migration
        // predates, plus the D3 code-not-FK GL default for stock receipts. PeriodLockDate was
        // renamed to FrozenAccountsDate (tasks.md 2.2, plan.md §2 literal name) by the
        // RenamePeriodLockDateToFrozenAccountsDate migration.
        builder.Property(c => c.FrozenAccountsDate).HasColumnType("date");
        builder.Property(c => c.AllowNegativeStock).HasColumnType("bit").HasDefaultValue(false);
        builder.Property(c => c.StockReceivedAccountCode).HasMaxLength(50);

        // Phase 4 additions (Task 4.3, decision D3): the buying-side GL defaults - Accounts
        // Payable, Input Tax Recoverable and the purchase price difference account.
        builder.Property(c => c.AccountsPayableAccountCode).HasMaxLength(50);
        builder.Property(c => c.InputTaxRecoverableAccountCode).HasMaxLength(50);
        builder.Property(c => c.PriceDifferenceAccountCode).HasMaxLength(50);
        builder.Property(c => c.CogsAccountCode).HasMaxLength(50);
        builder.Property(c => c.DefaultReceivableAccountCode).HasMaxLength(50);
        builder.Property(c => c.DefaultIncomeAccountCode).HasMaxLength(50);

        // Block B (Tasks 12.3-12.4, decision D3): the payroll-payable GL default. The Block C
        // migration adds the physical column; the mapping lands now so the model is complete.
        builder.Property(c => c.PayrollPayableAccountCode).HasMaxLength(50);

        builder.Property(c => c.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .HasConstraintName("FK_Company_Tenant")
            // plan.md §7.2's DDL specifies plain FK semantics (SQL Server default = NO ACTION);
            // EF's convention would silently turn this into ON DELETE CASCADE.
            .OnDelete(DeleteBehavior.Restrict);

        // RM-09: functional currency link to the global Currency catalog (nullable for legacy
        // rows; plain FK semantics = SQL Server default NO ACTION).
        builder.HasOne(c => c.Currency)
            .WithMany()
            .HasForeignKey(c => c.CurrencyId)
            .HasConstraintName("FK_Company_Currency")
            .OnDelete(DeleteBehavior.Restrict);

        // Constitution Article IV.1: every index on a tenant-scoped table leads with TenantId.
        builder.HasIndex(c => c.TenantId).HasDatabaseName("IX_Company_Tenant");
    }
}
