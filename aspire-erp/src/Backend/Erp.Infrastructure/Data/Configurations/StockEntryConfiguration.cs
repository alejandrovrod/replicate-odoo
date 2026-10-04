using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Stock voucher header (decision D1): no draft lifecycle - a row exists only once it is posted,
/// which is why <c>VoucherNo</c> is NOT NULL (assigned inside the posting transaction).
/// </summary>
public sealed class StockEntryConfiguration : IEntityTypeConfiguration<StockEntry>
{
    public void Configure(EntityTypeBuilder<StockEntry> builder)
    {
        builder.ToTable("StockEntry");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Optimistic concurrency (spec ST-06): store-generated rowversion token.
        builder.Property(s => s.RowVersion).IsRowVersion();

        builder.Property(s => s.EntryType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.PostingDate).HasColumnType("date").IsRequired();
        builder.Property(s => s.VoucherNo).HasMaxLength(100).IsRequired();
        builder.Property(s => s.IsCancelled).HasDefaultValue(false);
        builder.Property(s => s.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(s => s.Warehouse)
            .WithMany()
            .HasForeignKey(s => s.WarehouseId)
            .HasConstraintName("FK_StockEntry_Warehouse")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.TargetWarehouse)
            .WithMany()
            .HasForeignKey(s => s.TargetWarehouseId)
            .HasConstraintName("FK_StockEntry_TargetWarehouse")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(s => s.CompanyId)
            .HasConstraintName("FK_StockEntry_Company")
            .OnDelete(DeleteBehavior.Restrict);

        // Lines are part of the aggregate: delete the voucher, delete its lines.
        builder.HasMany(s => s.Items)
            .WithOne(i => i.StockEntry)
            .HasForeignKey(i => i.StockEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Gapless voucher lookup (Constitution III.4) - TenantId leads per Constitution IV.1.
        builder.HasIndex(s => new { s.TenantId, s.CompanyId, s.VoucherNo })
            .HasDatabaseName("IX_StockEntry_Tenant_Company_Voucher");
    }
}
