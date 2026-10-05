using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>Designation table (plan.md §1 DDL table 2): company FK with Restrict delete.</summary>
public sealed class DesignationConfiguration : IEntityTypeConfiguration<Designation>
{
    public void Configure(EntityTypeBuilder<Designation> builder)
    {
        builder.ToTable("Designation");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(d => d.DesignationName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.IsActive).HasDefaultValue(true);
        builder.Property(d => d.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(d => d.CompanyId)
            .HasConstraintName("FK_Designation_Company")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
