using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Idempotency reservation table (Constitution VI.4 / decision D7): the UNIQUE (TenantId, Key)
/// index is what makes a reservation atomic - two concurrent requests with the same key can never
/// both win the insert.
/// </summary>
public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecord");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(r => r.Key).HasMaxLength(100).IsRequired();
        builder.Property(r => r.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(r => r.ResponseStatus);
        builder.Property(r => r.ResponseBody);
        builder.Property(r => r.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // Constitution IV.1: TenantId leads the unique index - idempotency keys are per tenant.
        builder.HasIndex(r => new { r.TenantId, r.Key })
            .IsUnique()
            .HasDatabaseName("IX_IdempotencyRecord_Tenant_Key");
    }
}
