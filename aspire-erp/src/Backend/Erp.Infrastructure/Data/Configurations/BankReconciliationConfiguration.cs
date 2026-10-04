using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Reconciliation link (task 6.4): one allocation slice of a reconciled staging line.
/// Justified deviation from plan.md §1 DDL (no link table there) - multi-voucher allocation
/// (BN-04) needs a home for per-voucher amounts and GLEntry rows are append-only, so the link
/// lives outside both sides.
/// </summary>
public sealed class BankReconciliationConfiguration : IEntityTypeConfiguration<BankReconciliation>
{
    public void Configure(EntityTypeBuilder<BankReconciliation> builder)
    {
        builder.ToTable("BankReconciliation");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(l => l.CounterpartType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(l => l.AllocatedAmount)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.HasOne(l => l.BankTransaction)
            .WithMany()
            .HasForeignKey(l => l.BankTransactionId)
            .HasConstraintName("FK_BankReconciliation_BankTransaction")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => new { l.TenantId, l.BankTransactionId })
            .HasDatabaseName("IX_BankReconciliation_Tenant_Transaction");

        builder.HasIndex(l => new { l.TenantId, l.CounterpartType, l.CounterpartId })
            .HasDatabaseName("IX_BankReconciliation_Tenant_Counterpart");
    }
}
