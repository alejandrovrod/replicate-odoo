using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>plan.md §1: CRM Activity &amp; Follow-Up Log (FK cascade to Opportunity).</summary>
/// <remarks>
/// No temporal versioning: plan §1 DDL declares no SYSTEM_VERSIONING for CRMActivity (the
/// log itself IS the audit trail). The Opportunity-side relationship (cascade) is declared
/// once in <see cref="OpportunityConfiguration"/>; this side only carries the column rules.
/// Type persists as the enum NAME (AccountConfiguration RootType precedent).
/// </remarks>
public sealed class CRMActivityConfiguration : IEntityTypeConfiguration<CRMActivity>
{
    public void Configure(EntityTypeBuilder<CRMActivity> builder)
    {
        builder.ToTable("CRMActivity");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(a => a.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.Subject).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Content);
        builder.Property(a => a.ActivityDate).HasDefaultValueSql("SYSDATETIMEOFFSET()").IsRequired();
        builder.Property(a => a.NextFollowUpDate);

        builder.HasIndex(a => a.OpportunityId)
            .HasDatabaseName("IX_CRMActivity_Opportunity");
    }
}
