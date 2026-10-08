using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

public class PeriodClosingVoucherConfiguration : IEntityTypeConfiguration<PeriodClosingVoucher>
{
    public void Configure(EntityTypeBuilder<PeriodClosingVoucher> builder)
    {
        builder.ToTable("PeriodClosingVouchers");
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.VoucherNo).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => new { x.CompanyId, x.VoucherNo }).IsUnique();

        builder.Property(x => x.DocumentStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
