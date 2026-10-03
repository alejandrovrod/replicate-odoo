using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

public sealed class UOMConfiguration : IEntityTypeConfiguration<UOM>
{
    public void Configure(EntityTypeBuilder<UOM> builder)
    {
        builder.ToTable("UOM");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(u => u.UomName).HasMaxLength(50).IsRequired();
        builder.Property(u => u.Symbol).HasMaxLength(10).IsRequired();
        builder.Property(u => u.MustBeWholeNumber).HasDefaultValue(false);
        builder.Property(u => u.IsActive).HasDefaultValue(true);
    }
}
