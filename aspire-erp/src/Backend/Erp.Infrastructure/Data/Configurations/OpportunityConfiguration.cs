using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
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

        builder.HasIndex(o => new { o.TenantId, o.CompanyId, o.Stage, o.Status })
            .HasDatabaseName("IX_Opportunity_Tenant_Stage");
    }
}
