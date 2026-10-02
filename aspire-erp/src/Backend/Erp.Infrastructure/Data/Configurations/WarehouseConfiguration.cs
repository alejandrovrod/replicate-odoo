using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Warehouse table: hierarchical (self FK), company-scoped with the unique
/// (TenantId, CompanyId, Code) index and the mandatory linked stock account.
/// </summary>
public sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouse");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(w => w.Code).HasMaxLength(50).IsRequired();
        builder.Property(w => w.Name).HasMaxLength(150).IsRequired();
        builder.Property(w => w.IsGroup).HasDefaultValue(false);
        builder.Property(w => w.IsActive).HasDefaultValue(true);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(w => w.CompanyId)
            .HasConstraintName("FK_Warehouse_Company")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Parent)
            .WithMany(w => w.Children)
            .HasForeignKey(w => w.ParentWarehouseId)
            .HasConstraintName("FK_Warehouse_Parent")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.StockAccount)
            .WithMany()
            .HasForeignKey(w => w.StockAccountId)
            .HasConstraintName("FK_Warehouse_StockAccount")
            .OnDelete(DeleteBehavior.Restrict);

        // Constitution IV.1: TenantId leads the composite unique index.
        builder.HasIndex(w => new { w.TenantId, w.CompanyId, w.Code })
            .IsUnique()
            .HasDatabaseName("IX_Warehouse_Tenant_Company_Code");
    }
}
