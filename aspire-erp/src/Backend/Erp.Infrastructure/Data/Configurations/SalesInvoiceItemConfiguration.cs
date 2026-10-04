using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

public sealed class SalesInvoiceItemConfiguration : IEntityTypeConfiguration<SalesInvoiceItem>
{
    public void Configure(EntityTypeBuilder<SalesInvoiceItem> builder)
    {
        builder.ToTable("SalesInvoiceItem");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(i => i.Quantity).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(i => i.Rate).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(i => i.Amount).HasColumnType("decimal(18,4)").IsRequired();

        builder.HasOne(i => i.Item)
            .WithMany()
            .HasForeignKey(i => i.ItemId)
            .HasConstraintName("FK_SalesInvoiceItem_Item")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
