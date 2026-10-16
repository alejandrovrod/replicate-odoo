using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Tax-engine Fase 2: reusable tax-package catalog. The explicit configuration reproduces
/// the EXACT convention-mapped DDL the tables were created with (Title/Description
/// nvarchar(max), Rate/Amount decimal(18,2)) so adopting the configuration produces zero
/// diff on existing columns - only the Fase 2 columns (Category, ChargeType,
/// CalculationBasis) arrive as additive changes.
/// </summary>
public sealed class TaxTemplateConfiguration : IEntityTypeConfiguration<TaxTemplate>
{
    public void Configure(EntityTypeBuilder<TaxTemplate> builder)
    {
        builder.ToTable("TaxTemplates");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(t => t.Title).IsRequired();
        builder.Property(t => t.Category).HasMaxLength(20).IsRequired().HasDefaultValue("Sales");
        builder.Property(t => t.IsDefault).IsRequired().HasDefaultValue(false);

        builder.HasMany(t => t.Taxes)
            .WithOne(l => l.TaxTemplate)
            .HasForeignKey(l => l.TaxTemplateId)
            .HasConstraintName("FK_TaxTemplateItem_TaxTemplates_TaxTemplateId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.TenantId, t.CompanyId, t.Category, t.Title })
            .IsUnique()
            .HasDatabaseName("UQ_TaxTemplate_Tenant_Company_Category_Title");
    }
}

/// <summary>Tax-engine Fase 2: lines of a tax template (same DDL-reproduction note as above).</summary>
public sealed class TaxTemplateItemConfiguration : IEntityTypeConfiguration<TaxTemplateItem>
{
    public void Configure(EntityTypeBuilder<TaxTemplateItem> builder)
    {
        builder.ToTable("TaxTemplateItem");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(l => l.Description).IsRequired();
        builder.Property(l => l.Rate).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(l => l.Amount).HasColumnType("decimal(18,2)").IsRequired();

        builder.Property(l => l.ChargeType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValue(TaxChargeType.Add);
        builder.Property(l => l.CalculationBasis)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValue(TaxCalculationBasis.OnNetTotal);

        builder.HasOne(l => l.Account)
            .WithMany()
            .HasForeignKey(l => l.AccountId)
            .HasConstraintName("FK_TaxTemplateItem_Account_AccountId")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => l.TaxTemplateId).HasDatabaseName("IX_TaxTemplateItem_TaxTemplateId");
        builder.HasIndex(l => l.AccountId).HasDatabaseName("IX_TaxTemplateItem_AccountId");
    }
}
