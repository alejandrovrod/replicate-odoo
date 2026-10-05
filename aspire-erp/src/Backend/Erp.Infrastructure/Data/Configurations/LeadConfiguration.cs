using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>plan.md §1: inbound Lead with temporal history and the tenant/company/status index.</summary>
/// <remarks>
/// plan.md §1 DDL is explicit (<c>WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE =
/// dbo.LeadHistory))</c>), so Lead is system-versioned exactly like Customer/Account (EF hangs
/// temporal config off ToTable(...)). The composite UNIQUE on (TenantId, CompanyId, LeadCode)
/// backs the entity's "unique per tenant+company" contract (CustomerConfiguration
/// UQ_Customer_Tenant_Company_Code precedent); IX_Lead_Tenant_Status is the plan §1 read index.
/// Status/Source persist as their NVARCHAR names (LeadStatus string constants by design).
/// </remarks>
public sealed class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Lead", table =>
        {
            table.IsTemporal(t => t.UseHistoryTable("LeadHistory"));
        });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(l => l.LeadCode).HasMaxLength(50).IsRequired();
        builder.Property(l => l.LeadName).HasMaxLength(150).IsRequired();
        builder.Property(l => l.OrganizationName).HasMaxLength(150);
        builder.Property(l => l.Email).HasMaxLength(150);
        builder.Property(l => l.Phone).HasMaxLength(50);
        builder.Property(l => l.Source).HasMaxLength(50).IsRequired().HasDefaultValue("Website");
        builder.Property(l => l.Status).HasMaxLength(30).IsRequired().HasDefaultValue("Open");
        builder.Property(l => l.IsActive).HasDefaultValue(true);
        builder.Property(l => l.ExternalReference).HasMaxLength(100);

        builder.HasOne(l => l.Company)
            .WithMany()
            .HasForeignKey(l => l.CompanyId)
            .HasConstraintName("FK_Lead_Company")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.TenantId, l.CompanyId, l.LeadCode })
            .IsUnique()
            .HasDatabaseName("UQ_Lead_Tenant_Company_Code");

        builder.HasIndex(l => new { l.TenantId, l.CompanyId, l.Status })
            .HasDatabaseName("IX_Lead_Tenant_Status");
    }
}
