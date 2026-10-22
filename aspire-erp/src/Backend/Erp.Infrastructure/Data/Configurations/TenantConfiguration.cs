using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// plan.md §7.1: Tenant table (Name 100, Code 50 UNIQUE, IsActive, CreatedAt with DB defaults).
/// Tenant itself is the isolation boundary, so it is deliberately NOT tenant-scoped.
/// </summary>
public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenant");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Code).HasMaxLength(50).IsRequired();

        // Multi-tenant onboarding: lowercase DNS label for host-based resolution.
        builder.Property(t => t.Subdomain).HasMaxLength(64).IsRequired();

        // Tenant-selected workspace language; every existing tenant falls back to Spanish.
        builder.Property(t => t.DefaultLanguage)
            .HasMaxLength(5)
            .IsRequired()
            .HasDefaultValue(TenantLanguage.Default);

        builder.Property(t => t.IsActive).HasDefaultValue(true);
        builder.Property(t => t.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // plan.md §7.1: Code NVARCHAR(50) NOT NULL UNIQUE.
        builder.HasIndex(t => t.Code).IsUnique().HasDatabaseName("IX_Tenant_Code");

        // Onboarding invariant: one tenant per subdomain (the database is the final arbiter;
        // the handler pre-check exists only for a friendly 409).
        builder.HasIndex(t => t.Subdomain)
            .IsUnique()
            .HasDatabaseName("UQ_Tenants_Subdomain");
    }
}
