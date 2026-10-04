using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Workstation master (plan.md §1 DDL table 1): hourly labor/electricity/rent rates as
/// decimal(18,4), composite total as a computed column mirroring the DDL expression
/// <c>HourRateTotal AS (HourRateLabor + HourRateElectricity + HourRateRent)</c>.
/// </summary>
public sealed class WorkstationConfiguration : IEntityTypeConfiguration<Workstation>
{
    public void Configure(EntityTypeBuilder<Workstation> builder)
    {
        builder.ToTable("Workstation");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(w => w.WorkstationName).HasMaxLength(100).IsRequired();

        builder.Property(w => w.HourRateLabor).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0m);
        builder.Property(w => w.HourRateElectricity).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0m);
        builder.Property(w => w.HourRateRent).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0m);

        // Plan DDL computed column; the CLR side stays a pure recomputing getter (single source
        // of truth) with a private discard setter so EF can materialize the column on query -
        // without any setter, model validation fails at runtime.
        builder.Property(w => w.HourRateTotal)
            .HasComputedColumnSql("[HourRateLabor] + [HourRateElectricity] + [HourRateRent]", stored: false);

        builder.Property(w => w.IsActive).HasDefaultValue(true);
        builder.Property(w => w.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(w => w.CompanyId)
            .HasConstraintName("FK_Workstation_Company")
            .OnDelete(DeleteBehavior.Restrict);

        // Constitution Article IV.1: TenantId leads the composite index.
        builder.HasIndex(w => new { w.TenantId, w.CompanyId, w.WorkstationName })
            .HasDatabaseName("IX_Workstation_Tenant_Company_Name");
    }
}
