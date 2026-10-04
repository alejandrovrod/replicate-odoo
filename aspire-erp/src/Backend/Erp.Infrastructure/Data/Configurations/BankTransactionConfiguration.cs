using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Isolated staging line (plan.md §1 DDL literal): decimal(18,4) sides, non-negative CHECKs,
/// enum NAME status, rowversion token (scenario BN-07) and the tenant/account/status index.
/// </summary>
public sealed class BankTransactionConfiguration : IEntityTypeConfiguration<BankTransaction>
{
    public void Configure(EntityTypeBuilder<BankTransaction> builder)
    {
        builder.ToTable("BankTransaction", table =>
        {
            table.HasCheckConstraint("CK_Deposit_NonNegative", "[Deposit] >= 0");
            table.HasCheckConstraint("CK_Withdrawal_NonNegative", "[Withdrawal] >= 0");
        });

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Optimistic concurrency (scenario BN-07): store-generated rowversion token.
        builder.Property(t => t.RowVersion).IsRowVersion();

        builder.Property(t => t.TransactionDate).HasColumnType("date").IsRequired();
        builder.Property(t => t.Deposit).HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(t => t.Withdrawal).HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(t => t.Currency).HasMaxLength(3).IsRequired().HasDefaultValue("USD");
        builder.Property(t => t.Description).HasMaxLength(500).IsRequired();
        builder.Property(t => t.ReferenceNumber).HasMaxLength(100);
        builder.Property(t => t.TransactionId).HasMaxLength(100);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(BankTransactionStatus.Unreconciled)
            .IsRequired();

        builder.Property(t => t.AllocatedAmount)
            .HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(t => t.ClearanceDate).HasColumnType("date");
        builder.Property(t => t.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(t => t.BankAccount)
            .WithMany()
            .HasForeignKey(t => t.BankAccountId)
            .HasConstraintName("FK_BankTransaction_BankAccount")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(t => t.CompanyId)
            .HasConstraintName("FK_BankTransaction_Company")
            .OnDelete(DeleteBehavior.Restrict);

        // Plan §1 staging workbench lookup - TenantId leads per Constitution IV.1.
        builder.HasIndex(t => new { t.TenantId, t.BankAccountId, t.Status })
            .HasDatabaseName("IX_BankTransaction_Tenant_Account_Status");
    }
}
