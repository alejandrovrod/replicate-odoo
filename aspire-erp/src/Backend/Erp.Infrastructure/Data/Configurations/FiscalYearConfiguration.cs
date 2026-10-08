using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Plan.md §2.1: Fiscal Year master — <c>CK_FY_Dates</c>, <c>UQ_FY_Company_Year</c>,
/// <c>IX_FY_Tenant_Company_Closed</c>. Overlap is enforced in the serializable repository scope
/// (a date-range overlap cannot be a CHECK); the unique index plus serializable inserts makes
/// concurrent overlapping creates serialize (spec FC-04).
/// </summary>
public sealed class FiscalYearConfiguration : IEntityTypeConfiguration<FiscalYear>
{
    public void Configure(EntityTypeBuilder<FiscalYear> builder)
    {
        builder.ToTable("FiscalYears", table =>
        {
            table.HasCheckConstraint("CK_FY_Dates", "[StartDate] < [EndDate]");
            table.HasCheckConstraint(
                "CK_FY_ClosedAt",
                "(([IsClosed] = 0 AND [ClosedAt] IS NULL) OR ([IsClosed] = 1))");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(x => x.YearName).HasMaxLength(20).IsRequired();
        builder.Property(x => x.StartDate).HasColumnType("date").IsRequired();
        builder.Property(x => x.EndDate).HasColumnType("date").IsRequired();
        builder.Property(x => x.IsClosed).HasColumnType("bit").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.ClosedAt);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .HasConstraintName("FK_FY_Company")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => new { x.CompanyId, x.YearName })
            .IsUnique()
            .HasDatabaseName("UQ_FY_Company_Year");

        // Constitution IV.1: TenantId leads every index on a tenant-scoped table.
        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.IsClosed })
            .HasDatabaseName("IX_FY_Tenant_Company_Closed")
            .IncludeProperties(x => new { x.StartDate, x.EndDate });
    }
}
