using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Global ISO currency catalog (RM-09). Deliberately NOT system-versioned (Supplier/Item
/// precedent for reference masters) and NOT tenant-scoped (ISO currencies are universal -
/// AppDbContext only filters <c>ITenantEntity</c> types, and this entity carries no TenantId).
/// </summary>
public sealed class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.ToTable("Currency");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Optimistic concurrency: store-generated rowversion token.
        builder.Property(c => c.RowVersion).IsRowVersion();

        builder.Property(c => c.Code).HasMaxLength(3).IsRequired();
        builder.Property(c => c.Symbol).HasMaxLength(10).IsRequired();
        builder.Property(c => c.FractionName).HasMaxLength(50).IsRequired().HasDefaultValue("");
        builder.Property(c => c.IsActive).HasDefaultValue(true);
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // Codes are unique GLOBALLY (currencies are shared across tenants).
        builder.HasIndex(c => c.Code)
            .IsUnique()
            .HasDatabaseName("UQ_Currency_Code");
    }
}
