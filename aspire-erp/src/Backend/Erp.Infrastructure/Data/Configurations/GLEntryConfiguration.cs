using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// plan.md §3.8 General Ledger table (verbatim): BIGINT IDENTITY key, positive-decimal CHECK
/// constraints (Constitution IV.3) and the covering
/// <c>IX_GLEntry_Tenant_Account_Date INCLUDE (Debit, Credit, VoucherType, VoucherNo)</c> index
/// (Constitution IV.1). The INSTEAD OF UPDATE/DELETE trigger is added by the migration itself
/// (Constitution III.2, database level).
/// </summary>
public sealed class GLEntryConfiguration : IEntityTypeConfiguration<GLEntry>
{
    public void Configure(EntityTypeBuilder<GLEntry> builder)
    {
        builder.ToTable("GLEntry", table =>
        {
            // plan.md §3.8 / Constitution IV.3.
            table.HasCheckConstraint("CK_Debit_Positive", "[Debit] >= 0");
            table.HasCheckConstraint("CK_Credit_Positive", "[Credit] >= 0");
        });

        // BIGINT IDENTITY(1,1) per plan §3.8 (numeric key = value generated on add by convention;
        // stated explicitly so the intent survives any convention change).
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id)
            .ValueGeneratedOnAdd();

        builder.Property(g => g.PostingDate).HasColumnType("date").IsRequired();
        builder.Property(g => g.Debit).HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(g => g.Credit).HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(g => g.VoucherType).HasMaxLength(50).IsRequired();
        builder.Property(g => g.VoucherNo).HasMaxLength(100).IsRequired();
        builder.Property(g => g.Remarks);
        builder.Property(g => g.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // plan.md §3.8 FK_GLEntry_Account (plain FK = NO ACTION, EF convention would cascade).
        builder.HasOne(g => g.Account)
            .WithMany()
            .HasForeignKey(g => g.AccountId)
            .HasConstraintName("FK_GLEntry_Account")
            .OnDelete(DeleteBehavior.Restrict);

        // Constitution IV.1 + plan §3.8: TenantId leads, with the plan's INCLUDE columns.
        builder.HasIndex(g => new { g.TenantId, g.AccountId, g.PostingDate })
            .HasDatabaseName("IX_GLEntry_Tenant_Account_Date")
            .IncludeProperties(g => new { g.Debit, g.Credit, g.VoucherType, g.VoucherNo });
    }
}
