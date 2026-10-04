using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>Payment voucher header (task 6.1): enum NAME type/status, rowversion token.</summary>
public sealed class PaymentEntryConfiguration : IEntityTypeConfiguration<PaymentEntry>
{
    public void Configure(EntityTypeBuilder<PaymentEntry> builder)
    {
        builder.ToTable("PaymentEntry");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Optimistic concurrency: concurrent reconciliations on one voucher lose instead of winning.
        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.Property(p => p.PaymentType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.PaymentDate).HasColumnType("date").IsRequired();
        builder.Property(p => p.PaidAmount).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(p => p.ReferenceNumber).HasMaxLength(100);

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(PaymentStatus.Unreconciled)
            .IsRequired();

        builder.Property(p => p.ClearanceDate).HasColumnType("date");
        builder.Property(p => p.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(p => p.BankAccount)
            .WithMany()
            .HasForeignKey(p => p.BankAccountId)
            .HasConstraintName("FK_PaymentEntry_BankAccount")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(p => p.CompanyId)
            .HasConstraintName("FK_PaymentEntry_Company")
            .OnDelete(DeleteBehavior.Restrict);

        // Allocations are part of the aggregate: delete the voucher, delete its slices.
        builder.HasMany(p => p.Allocations)
            .WithOne(a => a.PaymentEntry)
            .HasForeignKey(a => a.PaymentEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.TenantId, p.CompanyId, p.Status })
            .HasDatabaseName("IX_PaymentEntry_Tenant_Company_Status");
    }
}
