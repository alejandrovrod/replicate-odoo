using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// BOM workstation operation. The plan DDL defines no operations table (deliberate deviation -
/// MF-01's timed WS-01 step needs a home); duration is decimal(18,4) minutes, strictly positive.
/// </summary>
public sealed class BomOperationConfiguration : IEntityTypeConfiguration<BomOperation>
{
    public void Configure(EntityTypeBuilder<BomOperation> builder)
    {
        builder.ToTable("BomOperation", table =>
            table.HasCheckConstraint("CK_BomOperation_Duration", "[DurationMinutes] > 0.0000"));

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(o => o.Description).HasMaxLength(500);
        builder.Property(o => o.DurationMinutes).HasColumnType("decimal(18,4)").IsRequired();

        builder.HasOne(o => o.Workstation)
            .WithMany()
            .HasForeignKey(o => o.WorkstationId)
            .HasConstraintName("FK_BomOperation_Workstation")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
