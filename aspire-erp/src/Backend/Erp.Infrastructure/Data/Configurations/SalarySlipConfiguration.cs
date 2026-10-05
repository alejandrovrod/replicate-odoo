using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Salary slip table (Task 12.3, plan.md §1 DDL table "SalarySlip"): one row per (entry,
/// employee) with the stored HR-01 amounts. Aggregate child of PayrollEntry (the BOM
/// precedent): no TenantId of its own, reached through the entry - so no tenant-filter
/// bypass check applies. The HR-06 unique pair is DB-enforced (UQ_SalarySlip_Entry_Employee).
/// </summary>
public sealed class SalarySlipConfiguration : IEntityTypeConfiguration<SalarySlip>
{
    public void Configure(EntityTypeBuilder<SalarySlip> builder)
    {
        builder.ToTable("SalarySlip");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Optimistic concurrency: store-generated rowversion token (same precedent as WorkOrder).
        builder.Property(s => s.RowVersion).IsRowVersion();

        builder.Property(s => s.SlipNumber).HasMaxLength(30).IsRequired();
        builder.Property(s => s.PaymentDays).IsRequired();
        builder.Property(s => s.AbsentDays).IsRequired();

        builder.Property(s => s.GrossPay).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(s => s.TotalDeductions).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(s => s.NetPay).HasColumnType("decimal(18,4)").IsRequired();

        // Status persisted as the enum NAME (NVARCHAR(20)), like SalaryComponentType.
        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(SalarySlipStatus.Draft);

        builder.HasOne(s => s.Employee)
            .WithMany()
            .HasForeignKey(s => s.EmployeeId)
            .HasConstraintName("FK_SalarySlip_Employee")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Lines)
            .WithOne(l => l.Slip)
            .HasForeignKey(l => l.SlipId)
            .OnDelete(DeleteBehavior.Cascade);

        // Spec HR-06: exactly one slip per (entry, employee) - the database is the authority
        // for the race the row lock serializes; the repository pre-check is 409 UX only.
        builder.HasIndex(s => new { s.PayrollEntryId, s.EmployeeId })
            .IsUnique()
            .HasDatabaseName("UQ_SalarySlip_Entry_Employee");
    }
}
