using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Plan.md §2.3: derived closing-line snapshot (audit of WHAT was zeroed).
/// </summary>
public sealed class PeriodClosingVoucherLineConfiguration : IEntityTypeConfiguration<PeriodClosingVoucherLine>
{
    public void Configure(EntityTypeBuilder<PeriodClosingVoucherLine> builder)
    {
        builder.ToTable("PeriodClosingVoucherLines", table =>
        {
            table.HasCheckConstraint(
                "CK_PCVL_NonNeg",
                "[Debit] >= 0 AND [Credit] >= 0 AND ([Debit] > 0 OR [Credit] > 0)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(x => x.Debit).HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(x => x.Credit).HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();

        builder.HasOne(x => x.Voucher)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.VoucherId)
            .HasConstraintName("FK_PCVL_Voucher")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Account)
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .HasConstraintName("FK_PCVL_Account")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => x.VoucherId)
            .HasDatabaseName("IX_PCVL_Voucher")
            .IncludeProperties(x => new { x.AccountId, x.Debit, x.Credit });
    }
}
