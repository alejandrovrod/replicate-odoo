using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Plan.md §2.2 full rewrite (spec §7 audit): the original table had no FKs, no CHECKs and no
/// filtered unique — replaced with <c>FiscalYearId</c> link, <c>IdempotencyKey</c>, status CHECK,
/// the filtered <c>UQ_One_Submitted_Close_Per_Year</c> (exactly one live close per year, FC-05),
/// <c>UQ_PCV_Company_VoucherNo</c>, filtered <c>UQ_PCV_Idempotency</c> and the covering list index.
/// </summary>
public class PeriodClosingVoucherConfiguration : IEntityTypeConfiguration<PeriodClosingVoucher>
{
    public void Configure(EntityTypeBuilder<PeriodClosingVoucher> builder)
    {
        builder.ToTable("PeriodClosingVouchers", table =>
        {
            table.HasCheckConstraint(
                "CK_PCV_Status",
                "[DocumentStatus] IN ('Draft','Submitted','Cancelled')");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(x => x.VoucherNo).IsRequired().HasMaxLength(50);
        builder.Property(x => x.PostingDate).HasColumnType("date").IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100);
        builder.Property(x => x.Remarks).HasColumnType("nvarchar(max)");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.Property(x => x.DocumentStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .HasConstraintName("FK_PCV_Company")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.FiscalYear)
            .WithMany()
            .HasForeignKey(x => x.FiscalYearId)
            .HasConstraintName("FK_PCV_FiscalYear")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.RetainedEarningsAccount)
            .WithMany()
            .HasForeignKey(x => x.RetainedEarningsAccountId)
            .HasConstraintName("FK_PCV_Retained")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(x => x.Lines)
            .WithOne(x => x.Voucher)
            .HasForeignKey(x => x.VoucherId)
            .HasConstraintName("FK_PCVL_Voucher")
            .OnDelete(DeleteBehavior.Cascade);

        // Exactly one live close per year (Cancelled rows don't count — re-close needs a new
        // voucher). Backstop for the FC-10 parallel-submit race.
        builder.HasIndex(x => new { x.CompanyId, x.FiscalYearId })
            .IsUnique()
            .HasFilter("[DocumentStatus] = 'Submitted'")
            .HasDatabaseName("UQ_One_Submitted_Close_Per_Year");

        builder.HasIndex(x => new { x.CompanyId, x.VoucherNo })
            .IsUnique()
            .HasDatabaseName("UQ_PCV_Company_VoucherNo");

        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL")
            .HasDatabaseName("UQ_PCV_Idempotency");

        // Constitution IV.1: TenantId leads the covering list index.
        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.DocumentStatus, x.PostingDate })
            .HasDatabaseName("IX_PCV_Tenant_Company_Status_Date")
            .IncludeProperties(x => new { x.FiscalYearId, x.VoucherNo });
    }
}
