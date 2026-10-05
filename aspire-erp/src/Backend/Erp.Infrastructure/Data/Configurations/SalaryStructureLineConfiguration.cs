using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>Structure component line (Task 12.2): amount/percentage check constraints.</summary>
public sealed class SalaryStructureLineConfiguration : IEntityTypeConfiguration<SalaryStructureLine>
{
    public void Configure(EntityTypeBuilder<SalaryStructureLine> builder)
    {
        builder.ToTable("SalaryStructureLine", table =>
        {
            table.HasCheckConstraint("CK_StructureLine_Amount", "[Amount] >= 0.0000");
            table.HasCheckConstraint(
                "CK_StructureLine_Percentage",
                "[PercentageOfBase] IS NULL OR [PercentageOfBase] >= 0.00");
        });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(l => l.Amount).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0m);
        builder.Property(l => l.PercentageOfBase).HasColumnType("decimal(5,2)").IsRequired(false);

        builder.HasOne(l => l.Component)
            .WithMany()
            .HasForeignKey(l => l.ComponentId)
            .HasConstraintName("FK_StructureLine_Component")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
