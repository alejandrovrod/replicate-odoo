using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Salary slip line table (Task 12.3 - justified addition the plan DDL lacks, see
/// <see cref="SalarySlipLine"/>): the itemized stub rows. Aggregate child of SalarySlip (no
/// TenantId of its own, the BomItem precedent); delete the slip, delete its rows (Cascade).
/// </summary>
public sealed class SalarySlipLineConfiguration : IEntityTypeConfiguration<SalarySlipLine>
{
    public void Configure(EntityTypeBuilder<SalarySlipLine> builder)
    {
        builder.ToTable("SalarySlipLine");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(l => l.ComponentName).HasMaxLength(100).IsRequired();

        // ComponentType snapshot persisted as the enum NAME (NVARCHAR(20) per the plan DDL).
        builder.Property(l => l.ComponentType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(l => l.Amount).HasColumnType("decimal(18,4)").IsRequired();

        builder.HasOne(l => l.Component)
            .WithMany()
            .HasForeignKey(l => l.ComponentId)
            .HasConstraintName("FK_SalarySlipLine_Component")
            .OnDelete(DeleteBehavior.Restrict);

        // Tenant-led slip lookup index (Constitution IV.1 applies to tenant tables; this child
        // table carries no TenantId, so the index leads with the aggregate key instead).
        builder.HasIndex(l => l.SlipId)
            .HasDatabaseName("IX_SalarySlipLine_Slip");
    }
}
