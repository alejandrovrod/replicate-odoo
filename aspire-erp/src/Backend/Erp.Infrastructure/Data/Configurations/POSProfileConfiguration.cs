using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

public sealed class POSProfileConfiguration : IEntityTypeConfiguration<POSProfile>
{
    public void Configure(EntityTypeBuilder<POSProfile> builder)
    {
        builder.ToTable("POSProfile");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(p => p.ProfileName).HasMaxLength(100).IsRequired();

        builder.HasOne(p => p.Warehouse)
            .WithMany()
            .HasForeignKey(p => p.WarehouseId)
            .HasConstraintName("FK_POSProfile_Warehouse")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.CashAccount)
            .WithMany()
            .HasForeignKey(p => p.CashAccountId)
            .HasConstraintName("FK_POSProfile_CashAccount")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.CardClearingAccount)
            .WithMany()
            .HasForeignKey(p => p.CardClearingAccountId)
            .HasConstraintName("FK_POSProfile_CardAccount")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.IncomeAccount)
            .WithMany()
            .HasForeignKey(p => p.IncomeAccountId)
            .HasConstraintName("FK_POSProfile_IncomeAccount")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.WriteOffAccount)
            .WithMany()
            .HasForeignKey(p => p.WriteOffAccountId)
            .HasConstraintName("FK_POSProfile_WriteOffAccount")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
