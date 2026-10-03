using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>plan.md §7.3: hierarchical Account table with temporal history and the composite tenant index.</summary>
public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        // Constitution Article IV.2: Account is a master entity, so it MUST be system-versioned;
        // history rows land in dbo.AccountHistory. (EF 10 hangs temporal config off ToTable(...),
        // same as CompanyConfiguration.)
        builder.ToTable("Account", table =>
            table.IsTemporal(t => t.UseHistoryTable("AccountHistory")));

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Optimistic concurrency (specs ST-06/BY-06): store-generated rowversion token. Verified
        // to coexist with the temporal history table on SQL Server 2025 (rowversion is a regular
        // column; the period stays datetime2).
        builder.Property(a => a.RowVersion).IsRowVersion();

        builder.Property(a => a.AccountCode).HasMaxLength(50).IsRequired();
        builder.Property(a => a.AccountName).HasMaxLength(150).IsRequired();

        // plan.md §7.3 DDL: RootType NVARCHAR(20) ('Asset', 'Liability', ...) - persist the enum
        // NAME rather than the underlying int so history rows stay human-readable.
        builder.Property(a => a.RootType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // plan.md §7.3 DDL: Type NVARCHAR(50) NOT NULL ('Bank', 'Cash', 'Receivable', ...) -
        // Task 1.1. Same NAME conversion as RootType. The SQL DEFAULT ('Other', the CLR default
        // of AccountType) keeps the column NOT NULL for the 16 existing rows on ADD and lets the
        // raw-SQL dev seeds (scripts/seed-dev-*.sql, which do not list Type) keep inserting
        // valid values.
        builder.Property(a => a.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired()
            .HasDefaultValue(AccountType.Other);

        builder.Property(a => a.IsGroup).HasDefaultValue(false);
        builder.Property(a => a.ParentAccountId);

        builder.Property(a => a.Currency).HasMaxLength(3).IsRequired().HasDefaultValue("USD");
        builder.Property(a => a.IsActive).HasDefaultValue(true);

        // plan.md §7.3 FK_Account_Company: plain FK semantics = SQL Server default NO ACTION;
        // EF's convention for a required principal would silently become ON DELETE CASCADE.
        builder.HasOne(a => a.Company)
            .WithMany()
            .HasForeignKey(a => a.CompanyId)
            .HasConstraintName("FK_Account_Company")
            .OnDelete(DeleteBehavior.Restrict);

        // plan.md §7.3 FK_Account_Parent (self-referencing, plain FK = NO ACTION).
        builder.HasOne(a => a.Parent)
            .WithMany(a => a.Children)
            .HasForeignKey(a => a.ParentAccountId)
            .HasConstraintName("FK_Account_Parent")
            .OnDelete(DeleteBehavior.Restrict);

        // Constitution Article IV.1 + plan.md §7.3: TenantId leads the composite index.
        // plan.md §7.3 (authoritative DDL) requires it UNIQUE under the name
        // UQ_Account_Tenant_Company_Code: the database is the authority for duplicate codes, so
        // the handler's ExistsByCodeAsync pre-check (kept only for the 409 UX) can no longer be
        // raced by two concurrent inserts (AccountRepository.AddAsync translates the unique-index
        // violation back to duplicate_account_code).
        builder.HasIndex(a => new { a.TenantId, a.CompanyId, a.AccountCode })
            .IsUnique()
            .HasDatabaseName("UQ_Account_Tenant_Company_Code");
    }
}
