using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Salary component table (plan.md §1 DDL table 4): ComponentType persisted as the enum NAME
/// (NVARCHAR(20) per the plan DDL), the default-GL-account FK with Restrict delete.
/// </summary>
public sealed class SalaryComponentConfiguration : IEntityTypeConfiguration<SalaryComponent>
{
    public void Configure(EntityTypeBuilder<SalaryComponent> builder)
    {
        builder.ToTable("SalaryComponent");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(c => c.ComponentName).HasMaxLength(100).IsRequired();

        // Plan DDL: Type NVARCHAR(20) ('Earning', 'Deduction') - persist the enum NAME.
        builder.Property(c => c.ComponentType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.DependsOnPaymentDays).HasDefaultValue(false);
        builder.Property(c => c.IsTaxApplicable).HasDefaultValue(true);
        builder.Property(c => c.IsActive).HasDefaultValue(true);
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(c => c.CompanyId)
            .HasConstraintName("FK_SalaryComponent_Company")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(c => c.DefaultGLAccountId)
            .HasConstraintName("FK_SalaryComponent_Account")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
