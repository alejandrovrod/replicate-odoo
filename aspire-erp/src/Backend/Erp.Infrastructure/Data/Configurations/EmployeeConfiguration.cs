using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Employee master (plan.md §1 DDL table 3): system-versioned (temporal) per the plan DDL,
/// the (TenantId, CompanyId, EmployeeNumber) unique code, department/designation FKs with
/// Restrict delete, SalaryMode and Status persisted as their enum NAMEs (NVARCHAR per the
/// plan DDL), and the RowVersion optimistic token (spec AS-06 precedent).
/// </summary>
public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        // Constitution Article IV.2: master entity -> system-versioned (plan.md §1 DDL).
        builder.ToTable("Employee", table =>
            table.IsTemporal(t => t.UseHistoryTable("EmployeeHistory")));

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Optimistic concurrency: store-generated rowversion token (same precedent as Account).
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.Property(e => e.EmployeeNumber).HasMaxLength(50).IsRequired();
        builder.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.LastName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.WorkEmail).HasMaxLength(150).IsRequired();
        builder.Property(e => e.DateOfJoining).HasColumnType("date").IsRequired();
        builder.Property(e => e.DateOfRelieving).HasColumnType("date").IsRequired(false);
        builder.Property(e => e.BankName).HasMaxLength(100).IsRequired(false);
        builder.Property(e => e.BankAccountNumber).HasMaxLength(50).IsRequired(false);

        // Plan DDL: SalaryMode NVARCHAR(20) ('Bank', ...) - persist the enum NAME.
        builder.Property(e => e.SalaryMode)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(SalaryMode.Bank);

        // Plan DDL: Status NVARCHAR(30) ('Active', ...) - persist the enum NAME like AssetStatus.
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValue(EmploymentStatus.Active);

        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(e => e.Department)
            .WithMany()
            .HasForeignKey(e => e.DepartmentId)
            .HasConstraintName("FK_Employee_Department")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Designation)
            .WithMany()
            .HasForeignKey(e => e.DesignationId)
            .HasConstraintName("FK_Employee_Designation")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .HasConstraintName("FK_Employee_Company")
            .OnDelete(DeleteBehavior.Restrict);

        // Plan DDL UQ_Employee_Tenant_Company_Code: the database is the authority for duplicate
        // employee numbers, so any handler pre-check stays a 409 UX only (same note as Account).
        // Constitution Article IV.1: TenantId leads the composite unique index.
        builder.HasIndex(e => new { e.TenantId, e.CompanyId, e.EmployeeNumber })
            .IsUnique()
            .HasDatabaseName("UQ_Employee_Tenant_Company_Code");
    }
}
