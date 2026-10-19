using System.Text.Json;
using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>plan.md §1: commercial Opportunity with temporal history, CHECKs and RowVersion.</summary>
/// <remarks>
/// plan.md §1 DDL is explicit (<c>WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE =
/// dbo.OpportunityHistory))</c>), so Opportunity is system-versioned exactly like
/// Customer/Account. RowVersion (spec CRM-06 optimistic concurrency) coexists with the
/// temporal period on the same table - the Account precedent proves it works: rowversion is
/// a regular column, the period stays datetime2. CK_Opportunity_Amount and
/// CK_Opportunity_Probability mirror the plan DDL names literally; WeightedAmount is a
/// computed CLR property (Amount * Probability / 100, spec CRM-01), NOT a mapped column, so
/// no computed-column mapping is declared here. Stage/Status/OpportunityFrom persist as
/// their NVARCHAR names (string constants by design).
/// </remarks>
public sealed class OpportunityConfiguration : IEntityTypeConfiguration<Opportunity>
{
    public void Configure(EntityTypeBuilder<Opportunity> builder)
    {
        builder.ToTable("Opportunity", table =>
        {
            table.IsTemporal(t => t.UseHistoryTable("OpportunityHistory"));

            table.HasCheckConstraint("CK_Opportunity_Amount", "[OpportunityAmount] >= 0.0000");
            table.HasCheckConstraint("CK_Opportunity_Probability", "[Probability] >= 0.00 AND [Probability] <= 100.00");
        });

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Optimistic concurrency (spec CRM-06): store-generated rowversion token, same
        // coexistence with the temporal table as AccountConfiguration/CustomerConfiguration.
        builder.Property(o => o.RowVersion).IsRowVersion();

        builder.Property(o => o.OpportunityNumber).HasMaxLength(50).IsRequired();
        builder.Property(o => o.OpportunityFrom).HasMaxLength(20).IsRequired().HasDefaultValue("Lead");
        builder.Property(o => o.PartyName).HasMaxLength(150).IsRequired();
        builder.Property(o => o.Stage).HasMaxLength(30).IsRequired().HasDefaultValue("Prospecting");
        builder.Property(o => o.OpportunityAmount).HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(o => o.Probability).HasColumnType("decimal(5,2)").HasDefaultValue(10.00m).IsRequired();
        builder.Ignore(o => o.WeightedAmount);
        builder.Property(o => o.CurrencyId);
        builder.Property(o => o.Status).HasMaxLength(30).IsRequired().HasDefaultValue("Open");

        builder.HasOne(o => o.Company)
            .WithMany()
            .HasForeignKey(o => o.CompanyId)
            .HasConstraintName("FK_Opportunity_Company")
            .OnDelete(DeleteBehavior.Restrict);

        // RM-09: deal currency link to the global Currency catalog (nullable for legacy rows).
        builder.HasOne(o => o.Currency)
            .WithMany()
            .HasForeignKey(o => o.CurrencyId)
            .HasConstraintName("FK_Opportunity_Currency")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Activities)
            .WithOne(a => a.Opportunity)
            .HasForeignKey(a => a.OpportunityId)
            .HasConstraintName("FK_CRMActivity_Opportunity")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.Items)
            .WithOne(i => i.Opportunity)
            .HasForeignKey(i => i.OpportunityId)
            .HasConstraintName("FK_OpportunityItem_Opportunity")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.Quotations)
            .WithOne(q => q.Opportunity)
            .HasForeignKey(q => q.OpportunityId)
            .HasConstraintName("FK_Quotation_Opportunity")
            .OnDelete(DeleteBehavior.Restrict);

        // CRM/ERPNext parity: segmentation + multi-currency + normalized loss reason.
        // All nullable (legacy rows) with DB defaults matching the CLR defaults.
        builder.Property(o => o.ExchangeRate).HasColumnType("decimal(18,6)").HasDefaultValue(1m).IsRequired();
        builder.Property(o => o.BaseOpportunityAmount).HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(o => o.MarketSegment).HasMaxLength(100);
        builder.Property(o => o.TerritoryId);
        builder.Property(o => o.IndustryId);
        builder.Property(o => o.LossReasonId);

        // Competitors ride as a JSON string set (fixed small vocabulary, never queried by
        // element): conversion + comparer keep change tracking honest for the collection.
        var competitorsComparer = new ValueComparer<List<string>>(
            (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
            list => (list ?? new List<string>()).Aggregate(0, HashCode.Combine),
            list => (list ?? new List<string>()).ToList());
        builder.Property(o => o.Competitors)
            .HasConversion(
                value => JsonSerializer.Serialize(value ?? new List<string>(), (JsonSerializerOptions?)null),
                value => string.IsNullOrWhiteSpace(value)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(value, (JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(competitorsComparer);

        builder.HasOne(o => o.Territory)
            .WithMany()
            .HasForeignKey(o => o.TerritoryId)
            .HasConstraintName("FK_Opportunity_Territory")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Industry)
            .WithMany()
            .HasForeignKey(o => o.IndustryId)
            .HasConstraintName("FK_Opportunity_Industry")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.LossReasonEntry)
            .WithMany()
            .HasForeignKey(o => o.LossReasonId)
            .HasConstraintName("FK_Opportunity_LossReason")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => new { o.TenantId, o.CompanyId, o.Stage, o.Status })
            .HasDatabaseName("IX_Opportunity_Tenant_Stage");
    }
}
