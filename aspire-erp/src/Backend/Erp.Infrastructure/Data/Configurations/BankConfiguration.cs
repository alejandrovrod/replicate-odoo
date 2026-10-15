using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>bank-catalog spec §3: offline master bank catalog, system-versioned + tenant-led indexes.</summary>
public sealed class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    public void Configure(EntityTypeBuilder<Bank> builder)
    {
        builder.ToTable("Banks", table =>
            table.IsTemporal(t => t.UseHistoryTable("BanksHistory")));

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(b => b.CountryCode).HasMaxLength(2).IsRequired();
        builder.Property(b => b.Code).HasMaxLength(20).IsRequired();
        builder.Property(b => b.Name).HasMaxLength(150).IsRequired();
        builder.Property(b => b.SwiftBic).HasMaxLength(11);
        builder.Property(b => b.IsActive).HasDefaultValue(true);

        // Spec §3 UQ_Banks_Tenant_Country_Code: the seed converges on this (skip-if-exists).
        builder.HasIndex(b => new { b.TenantId, b.CountryCode, b.Code })
            .IsUnique()
            .HasDatabaseName("UQ_Banks_Tenant_Country_Code");

        // Constitution Article IV.1: every index on a tenant-scoped table leads with TenantId.
        // INCLUDE covers the autocomplete read (country filter -> code/name, no key lookup).
        builder.HasIndex(b => new { b.TenantId, b.CountryCode, b.IsActive })
            .HasDatabaseName("IX_Banks_Tenant_Country_Active")
            .IncludeProperties(b => new { b.Code, b.Name });
    }
}
