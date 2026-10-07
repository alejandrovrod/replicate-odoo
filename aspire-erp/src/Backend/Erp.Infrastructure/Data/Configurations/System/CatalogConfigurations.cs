using Erp.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations.System;

public class CatalogConfiguration : IEntityTypeConfiguration<Catalog>
{
    public void Configure(EntityTypeBuilder<Catalog> builder)
    {
        builder.HasKey(c => c.Id);
        
        builder.Property(c => c.Code).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        
        builder.HasIndex(c => new { c.CompanyId, c.Code }).IsUnique();
        
        // Let EF handle IsSystem
    }
}

public class CatalogItemConfiguration : IEntityTypeConfiguration<CatalogItem>
{
    public void Configure(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.HasKey(c => c.Id);
        
        builder.Property(c => c.Code).IsRequired().HasMaxLength(100);
        builder.Property(c => c.DefaultName).IsRequired().HasMaxLength(200);
        
        builder.HasOne(c => c.Catalog)
            .WithMany(c => c.Items)
            .HasForeignKey(c => c.CatalogId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasIndex(c => new { c.CatalogId, c.CompanyId, c.Code }).IsUnique();
    }
}

public class CatalogItemTranslationConfiguration : IEntityTypeConfiguration<CatalogItemTranslation>
{
    public void Configure(EntityTypeBuilder<CatalogItemTranslation> builder)
    {
        builder.HasKey(c => c.Id);
        
        builder.Property(c => c.LanguageCode).IsRequired().HasMaxLength(10);
        builder.Property(c => c.TranslatedName).IsRequired().HasMaxLength(200);
        
        builder.HasOne(c => c.CatalogItem)
            .WithMany(c => c.Translations)
            .HasForeignKey(c => c.CatalogItemId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasIndex(c => new { c.CatalogItemId, c.LanguageCode }).IsUnique();
    }
}
