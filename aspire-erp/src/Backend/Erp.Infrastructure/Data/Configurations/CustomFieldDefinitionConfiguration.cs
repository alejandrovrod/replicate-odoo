using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Custom field catalog table (extensibility-engine Fase 1): tenant-wide definitions,
/// unique (TenantId, EntityName, FieldName). Config rows, never ledger data - hard delete
/// is allowed (the CRUD handler deletes; validation ignores inactive rows).
/// </summary>
public sealed class CustomFieldDefinitionConfiguration : IEntityTypeConfiguration<CustomFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CustomFieldDefinition> builder)
    {
        builder.ToTable("CustomFieldDefinition");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(d => d.EntityName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.FieldName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.FieldType).HasMaxLength(30).IsRequired();
        builder.Property(d => d.IsRequired).IsRequired().HasDefaultValue(false);
        builder.Property(d => d.DefaultValue).HasColumnType("NVARCHAR(MAX)");
        builder.Property(d => d.ValidationJsonSchema).HasColumnType("NVARCHAR(MAX)");
        builder.Property(d => d.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(d => d.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // Constitution Article IV.1: TenantId leads the composite index, UNIQUE per
        // (tenant, entity, field) - the database is the authority against raced duplicates
        // (the handler's ExistsAsync pre-check keeps the 409 UX).
        builder.HasIndex(d => new { d.TenantId, d.EntityName, d.FieldName })
            .IsUnique()
            .HasDatabaseName("UQ_CustomFieldDefinition_Tenant_Entity_Field");
    }
}
