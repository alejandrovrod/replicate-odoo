using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Journal Entry header (tasks.md 2.3): a transactional voucher with a Draft lifecycle, so Status
/// and Type are persisted as enum NAMES (nvarchar) and VoucherNo (JV-YYYY-NNNNN, Constitution
/// III.4) is assigned at creation inside the transaction - the exact shape of
/// <see cref="PurchaseOrderConfiguration"/>.
/// </summary>
/// <remarks>
/// Deliberately NOT temporal (the same class as the purchase documents: IV.2 reserves system
/// versioning for master entities such as Account/Company) and NOT append-only - the ledger rows
/// this voucher produces are the immutable part (GLEntry + Constitution III.2), while the header
/// must be able to move Draft -&gt; Submitted -&gt; Cancelled.
/// </remarks>
public sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("JournalEntry");

        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Optimistic concurrency (the same store-generated token as PurchaseOrder/StockEntry): a
        // concurrent submit/cancel between our load and our save fails loudly
        // (DbUpdateConcurrencyException -> ConcurrencyConflictException -> 409 concurrency_conflict)
        // instead of silently overwriting the newer status.
        builder.Property(j => j.RowVersion).IsRowVersion();

        builder.Property(j => j.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(j => j.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(j => j.PostingDate).HasColumnType("date").IsRequired();
        builder.Property(j => j.VoucherNo).HasMaxLength(100).IsRequired();
        builder.Property(j => j.UserRemark);
        builder.Property(j => j.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(j => j.CompanyId)
            .HasConstraintName("FK_JournalEntry_Company")
            .OnDelete(DeleteBehavior.Restrict);

        // Lines are part of the aggregate: delete the header, delete its lines.
        builder.HasMany(j => j.Lines)
            .WithOne(l => l.JournalEntry)
            .HasForeignKey(l => l.JournalEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Gapless JV numbering (Constitution III.4) + voucher drill-down: TenantId leads
        // (Constitution IV.1), CompanyId scopes the per-company/per-year sequence, VoucherNo is
        // the column the SELECT MAX ... LIKE 'JV-YYYY-%' statement probes.
        builder.HasIndex(j => new { j.TenantId, j.CompanyId, j.VoucherNo })
            .HasDatabaseName("IX_JournalEntry_Tenant_Company_Voucher");

        // List read (GET /journal-entries): newest first inside one company.
        builder.HasIndex(j => new { j.TenantId, j.CompanyId, j.CreatedAt })
            .HasDatabaseName("IX_JournalEntry_Tenant_Company_CreatedAt");
    }
}
