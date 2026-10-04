using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Depreciation schedule lines (Task 10.3, plan.md §1 DDL table 3): lines of the Asset aggregate
/// (cascade delete), the positive-amount CHECK and the due-date lookup index. Status persists as
/// its enum NAME (NVARCHAR(20)); see <see cref="AssetDepreciationSchedule"/> for why Status
/// replaces the plan's IsBooked BIT.
/// </summary>
public sealed class AssetDepreciationScheduleConfiguration : IEntityTypeConfiguration<AssetDepreciationSchedule>
{
    public void Configure(EntityTypeBuilder<AssetDepreciationSchedule> builder)
    {
        builder.ToTable("AssetDepreciationSchedule", table =>
        {
            table.HasCheckConstraint(
                "CK_DepSchedule_Amount",
                "[DepreciationAmount] > 0.0000");
        });

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(s => s.ScheduleDate).HasColumnType("date").IsRequired();
        builder.Property(s => s.DepreciationAmount).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(s => s.AccumulatedDepreciationAfter).HasColumnType("decimal(18,4)").IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(AssetScheduleStatus.Scheduled);

        builder.HasOne(s => s.Asset)
            .WithMany()
            .HasForeignKey(s => s.AssetId)
            .HasConstraintName("FK_DepSchedule_Asset")
            .OnDelete(DeleteBehavior.Cascade);

        // Plan DDL due-date lookup, with Status in place of the replaced IsBooked BIT.
        builder.HasIndex(s => new { s.ScheduleDate, s.Status })
            .HasDatabaseName("IX_AssetDepSchedule_Date");
    }
}
