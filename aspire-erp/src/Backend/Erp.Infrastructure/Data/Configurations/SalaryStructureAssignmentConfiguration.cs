using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Structure assignment table (Task 12.2 - added table, see SalaryStructure): the
/// employee-to-structure binding window with the employee/structure FKs.
/// </summary>
public sealed class SalaryStructureAssignmentConfiguration : IEntityTypeConfiguration<SalaryStructureAssignment>
{
    public void Configure(EntityTypeBuilder<SalaryStructureAssignment> builder)
    {
        builder.ToTable("SalaryStructureAssignment", table =>
        {
            table.HasCheckConstraint(
                "CK_Assignment_Dates",
                "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(a => a.EffectiveFrom).HasColumnType("date").IsRequired();
        builder.Property(a => a.EffectiveTo).HasColumnType("date").IsRequired(false);
        builder.Property(a => a.IsActive).HasDefaultValue(true);

        builder.HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeId)
            .HasConstraintName("FK_Assignment_Employee")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Structure)
            .WithMany()
            .HasForeignKey(a => a.StructureId)
            .HasConstraintName("FK_Assignment_Structure")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(a => a.CompanyId)
            .HasConstraintName("FK_Assignment_Company")
            .OnDelete(DeleteBehavior.Restrict);

        // Tenant-led employee lookup index (Constitution IV.1).
        builder.HasIndex(a => new { a.TenantId, a.EmployeeId, a.EffectiveFrom })
            .HasDatabaseName("IX_Assignment_Tenant_Employee");
    }
}
