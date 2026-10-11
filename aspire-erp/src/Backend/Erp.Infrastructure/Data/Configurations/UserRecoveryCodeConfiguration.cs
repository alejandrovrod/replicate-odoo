using Erp.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Module 15-user-profile: single-use MFA backup codes (spec §3.2). Tenant-scoped like every
/// other entity (Constitution Article II.1), so the global query filter applies automatically.
/// </summary>
public sealed class UserRecoveryCodeConfiguration : IEntityTypeConfiguration<UserRecoveryCode>
{
    public void Configure(EntityTypeBuilder<UserRecoveryCode> builder)
    {
        builder.ToTable("UserRecoveryCodes");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // SHA-256 hex is always 64 chars.
        builder.Property(c => c.CodeHash).HasMaxLength(64).IsRequired();
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasIndex(c => c.UserId).HasDatabaseName("IX_UserRecoveryCodes_UserId");
    }
}
