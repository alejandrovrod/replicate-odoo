using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// plan.md §7.2: Company table (multi-company per tenant) with FK to Tenant.
/// </summary>
public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        // Constitution Article IV.2: master entity Company must be system-versioned. plan.md §7's
        // DDL omits this for Company - the Constitution overrides the plan. In EF 10 the temporal
        // configuration hangs off the ToTable(...) builder.
        builder.ToTable("Company", table =>
            table.IsTemporal(t => t.UseHistoryTable("CompanyHistory")));

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
        builder.Property(c => c.DefaultCurrency).HasMaxLength(3).IsRequired().HasDefaultValue("USD");
        builder.Property(c => c.TaxId).HasMaxLength(50).IsRequired();
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .HasConstraintName("FK_Company_Tenant")
            // plan.md §7.2's DDL specifies plain FK semantics (SQL Server default = NO ACTION);
            // EF's convention would silently turn this into ON DELETE CASCADE.
            .OnDelete(DeleteBehavior.Restrict);

        // Constitution Article IV.1: every index on a tenant-scoped table leads with TenantId.
        builder.HasIndex(c => c.TenantId).HasDatabaseName("IX_Company_Tenant");
    }
}
