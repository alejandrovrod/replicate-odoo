using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>Heuristic rule header (plan.md §1 DDL literal): enum NAME condition, defaults.</summary>
public sealed class BankTransactionRuleConfiguration : IEntityTypeConfiguration<BankTransactionRule>
{
    public void Configure(EntityTypeBuilder<BankTransactionRule> builder)
    {
        builder.ToTable("BankTransactionRule");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(r => r.RuleName).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Priority).HasDefaultValue(1).IsRequired();

        builder.Property(r => r.ConditionType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.Pattern).HasMaxLength(255).IsRequired();
        builder.Property(r => r.TargetPartyType).HasMaxLength(50);
        builder.Property(r => r.AutoCreateVoucher).HasDefaultValue(false);
        builder.Property(r => r.IsActive).HasDefaultValue(true);

        builder.HasOne(r => r.BankAccount)
            .WithMany()
            .HasForeignKey(r => r.BankAccountId)
            .HasConstraintName("FK_BankTransactionRule_BankAccount")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(r => r.CompanyId)
            .HasConstraintName("FK_BankTransactionRule_Company")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.TenantId, r.CompanyId, r.Priority })
            .HasDatabaseName("IX_BankTransactionRule_Tenant_Company_Priority");
    }
}
