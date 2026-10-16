using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Operational workflow catalog (Fase 5): one tenant-wide definition per document type,
/// with states and transitions cascade-deleted with it. Documents reference states by
/// free-text name (<c>WorkflowState</c>), never by FK - deleting a flow unguards without
/// touching a single document row.
/// </summary>
public sealed class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
    {
        builder.ToTable("WorkflowDefinition");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(d => d.DocumentType).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Name).HasMaxLength(100).IsRequired();
        builder.Property(d => d.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(d => d.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasMany(d => d.States)
            .WithOne(s => s.WorkflowDefinition)
            .HasForeignKey(s => s.WorkflowDefinitionId)
            .HasConstraintName("FK_WorkflowState_Definition")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.Transitions)
            .WithOne(t => t.WorkflowDefinition)
            .HasForeignKey(t => t.WorkflowDefinitionId)
            .HasConstraintName("FK_WorkflowTransition_Definition")
            .OnDelete(DeleteBehavior.Cascade);

        // Constitution Article IV.1: TenantId leads the composite index, UNIQUE per
        // (tenant, document type, name) - the database is the authority against raced
        // duplicates (the handler's ExistsNameAsync pre-check keeps the 409 UX).
        builder.HasIndex(d => new { d.TenantId, d.DocumentType, d.Name })
            .IsUnique()
            .HasDatabaseName("UQ_Workflow_Tenant_Type_Name");
    }
}

/// <summary>Workflow states and transitions (child tables, cascade with the definition).</summary>
public sealed class WorkflowStateDefinitionConfiguration : IEntityTypeConfiguration<WorkflowStateDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowStateDefinition> builder)
    {
        builder.ToTable("WorkflowStateDefinition");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(s => s.StateName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.IsInitial).IsRequired().HasDefaultValue(false);
        builder.Property(s => s.CanSubmit).IsRequired().HasDefaultValue(false);

        builder.HasIndex(s => s.WorkflowDefinitionId)
            .HasDatabaseName("IX_WorkflowState_DefinitionId");
    }
}

public sealed class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
    {
        builder.ToTable("WorkflowTransition");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(t => t.FromState).HasMaxLength(100).IsRequired();
        builder.Property(t => t.ToState).HasMaxLength(100).IsRequired();
        builder.Property(t => t.ActionLabel).HasMaxLength(100).IsRequired();
        builder.Property(t => t.AllowedRole).HasMaxLength(100).IsRequired().HasDefaultValue(string.Empty);

        builder.HasIndex(t => t.WorkflowDefinitionId)
            .HasDatabaseName("IX_WorkflowTransition_DefinitionId");
    }
}

/// <summary>Applied-preset record: at most one row per tenant.</summary>
public sealed class TenantPresetConfiguration : IEntityTypeConfiguration<TenantPreset>
{
    public void Configure(EntityTypeBuilder<TenantPreset> builder)
    {
        builder.ToTable("TenantPreset");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(t => t.PresetKey).HasMaxLength(50).IsRequired();
        builder.Property(t => t.AppliedAt).IsRequired();

        builder.HasIndex(t => t.TenantId)
            .IsUnique()
            .HasDatabaseName("UQ_TenantPreset_Tenant");
    }
}
