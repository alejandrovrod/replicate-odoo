using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Payment voucher header (task 6.1, spec R-12): enum NAME type/status/document-status,
/// rowversion token, gapless voucher number and the PE-06 direction CHECK.
/// </summary>
public sealed class PaymentEntryConfiguration : IEntityTypeConfiguration<PaymentEntry>
{
    public void Configure(EntityTypeBuilder<PaymentEntry> builder)
    {
        builder.ToTable("PaymentEntry", table =>
        {
            // Defense in depth for PE-06 (the application guard owns the rule; the database
            // backstops it): Receive ↔ Customer, Pay ↔ Supplier/Employee, InternalTransfer
            // (ERPNext parity) carries no counterparty - PaidFrom/PaidTo own its legs.
            table.HasCheckConstraint(
                "CK_PaymentEntry_Direction",
                "([PaymentType] = 'Receive' AND [PartyType] = 'Customer') OR ([PaymentType] = 'Pay' AND [PartyType] IN ('Supplier', 'Employee')) OR ([PaymentType] = 'InternalTransfer')");
        });

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

        builder.Property(p => p.PartyType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.PartyId).IsRequired();

        builder.Property(p => p.VoucherNo).HasMaxLength(30).IsRequired().HasDefaultValue("");

        builder.Property(p => p.PaymentDate).HasColumnType("date").IsRequired();
        builder.Property(p => p.PaidAmount).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(p => p.UnallocatedAmount).HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(p => p.ReferenceNumber).HasMaxLength(100);

        // ERPNext-parity columns (Phase 6): all NOT NULL with server defaults so the
        // migration is non-destructive on existing rows.
        builder.Property(p => p.PartyName).HasMaxLength(150).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(p => p.ModeOfPayment).HasMaxLength(100).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(p => p.PaidFromAccountId);
        builder.Property(p => p.PaidFromAccountCurrency).HasMaxLength(3).IsRequired().HasDefaultValue("USD");
        builder.Property(p => p.PaidToAccountId);
        builder.Property(p => p.PaidToAccountCurrency).HasMaxLength(3).IsRequired().HasDefaultValue("USD");
        builder.Property(p => p.SourceExchangeRate).HasColumnType("decimal(18,6)").HasDefaultValue(1m).IsRequired();
        builder.Property(p => p.BasePaidAmount).HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(p => p.ReceivedAmount).HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(p => p.TargetExchangeRate).HasColumnType("decimal(18,6)").HasDefaultValue(1m).IsRequired();
        builder.Property(p => p.BaseReceivedAmount).HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(p => p.TotalAllocatedAmount).HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(p => p.DifferenceAmount).HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(p => p.ReferenceDate).HasColumnType("date");
        builder.Property(p => p.CostCenterId);
        builder.Property(p => p.ProjectId);
        builder.Property(p => p.Remarks).HasMaxLength(500).IsRequired().HasDefaultValue(string.Empty);

        builder.Property(p => p.DocumentStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(PaymentDocumentStatus.Draft)
            .IsRequired();

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
