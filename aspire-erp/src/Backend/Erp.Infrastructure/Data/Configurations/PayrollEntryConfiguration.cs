using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Payroll batch header (Task 12.3, plan.md §1 DDL table "PayrollEntry"): the gapless PE number,
/// the period window, the stored totals and the informational voucher links, plus the
/// RowVersion optimistic token (spec HR-06 precedent - every other aggregate carries it).
/// </summary>
public sealed class PayrollEntryConfiguration : IEntityTypeConfiguration<PayrollEntry>
{
    public void Configure(EntityTypeBuilder<PayrollEntry> builder)
    {
        builder.ToTable("PayrollEntry");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Optimistic concurrency: store-generated rowversion token (same precedent as WorkOrder).
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.Property(e => e.PayrollNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.StartDate).HasColumnType("date").IsRequired();
        builder.Property(e => e.EndDate).HasColumnType("date").IsRequired();
        builder.Property(e => e.PostingDate).HasColumnType("date").IsRequired();

        // Plan DDL: Status NVARCHAR(20) ('Draft', ...) - persist the enum NAME like AssetStatus.
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(PayrollEntryStatus.Draft);

        // Stored totals (decimal(18,4) - the HR-01 floor needs storage, not computation).
        builder.Property(e => e.TotalGrossPay).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(e => e.TotalDeductions).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(e => e.TotalNetPay).HasColumnType("decimal(18,4)").IsRequired();

        builder.Property(e => e.AccrualVoucherNo).HasMaxLength(30).IsRequired(false);
        builder.Property(e => e.PaymentVoucherNo).HasMaxLength(30).IsRequired(false);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .HasConstraintName("FK_PayrollEntry_Company")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Slips)
            .WithOne(s => s.PayrollEntry)
            .HasForeignKey(s => s.PayrollEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Constitution Article IV.1: TenantId leads the composite unique index.
        builder.HasIndex(e => new { e.TenantId, e.CompanyId, e.PayrollNumber })
            .IsUnique()
            .HasDatabaseName("UQ_PayrollEntry_Tenant_Company_Number");
    }
}
