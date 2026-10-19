using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>CRM/ERPNext parity: priced deal lines (plan §1 DDL literal names kept).</summary>
public sealed class OpportunityItemConfiguration : IEntityTypeConfiguration<OpportunityItem>
{
    public void Configure(EntityTypeBuilder<OpportunityItem> builder)
    {
        builder.ToTable("OpportunityItem");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(i => i.ItemName).HasMaxLength(150).IsRequired();
        builder.Property(i => i.Quantity).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(i => i.Rate).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(i => i.DiscountPercentage).HasColumnType("decimal(5,2)").IsRequired().HasDefaultValue(0m);
        builder.Ignore(i => i.Amount);

        builder.HasOne(i => i.Opportunity)
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OpportunityId)
            .HasConstraintName("FK_OpportunityItem_Opportunity")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Item)
            .WithMany()
            .HasForeignKey(i => i.ItemId)
            .HasConstraintName("FK_OpportunityItem_Item")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.TenantId, i.OpportunityId })
            .HasDatabaseName("IX_OpportunityItem_Tenant_Opportunity");
    }
}

/// <summary>CRM/ERPNext parity: normalized loss-reason catalog (unique code per tenant).</summary>
public sealed class OpportunityLossReasonConfiguration : IEntityTypeConfiguration<OpportunityLossReason>
{
    public void Configure(EntityTypeBuilder<OpportunityLossReason> builder)
    {
        builder.ToTable("OpportunityLossReason");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(r => r.Code).HasMaxLength(50).IsRequired();
        builder.Property(r => r.Name).HasMaxLength(150).IsRequired();
        builder.Property(r => r.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(r => new { r.TenantId, r.Code })
            .IsUnique()
            .HasDatabaseName("UQ_OpportunityLossReason_Tenant_Code");
    }
}

/// <summary>CRM segmentation parity: sales-territory master (read-only catalog).</summary>
public sealed class TerritoryConfiguration : IEntityTypeConfiguration<Territory>
{
    public void Configure(EntityTypeBuilder<Territory> builder)
    {
        builder.ToTable("Territory");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(t => t.Code).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(150).IsRequired();
        builder.Property(t => t.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(t => new { t.TenantId, t.Code })
            .IsUnique()
            .HasDatabaseName("UQ_Territory_Tenant_Code");
    }
}

/// <summary>CRM segmentation parity: industry master (read-only catalog).</summary>
public sealed class IndustryConfiguration : IEntityTypeConfiguration<Industry>
{
    public void Configure(EntityTypeBuilder<Industry> builder)
    {
        builder.ToTable("Industry");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(t => t.Code).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(150).IsRequired();
        builder.Property(t => t.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(t => new { t.TenantId, t.Code })
            .IsUnique()
            .HasDatabaseName("UQ_Industry_Tenant_Code");
    }
}

/// <summary>
/// CRM/ERPNext parity: deal linkage on quotations. Only the new FK is declared -
/// everything else stays on EF conventions (the pre-existing snapshot mapping is
/// untouched, so this migration adds exactly one column + FK).
/// </summary>
public sealed class QuotationConfiguration : IEntityTypeConfiguration<Quotation>
{
    public void Configure(EntityTypeBuilder<Quotation> builder)
    {
        builder.Property(q => q.OpportunityId);

        builder.HasOne(q => q.Opportunity)
            .WithMany(o => o.Quotations)
            .HasForeignKey(q => q.OpportunityId)
            .HasConstraintName("FK_Quotation_Opportunity")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
