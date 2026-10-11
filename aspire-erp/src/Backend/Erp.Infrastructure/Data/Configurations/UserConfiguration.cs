using Erp.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Module 15-user-profile: maps the MFA/lockout columns onto the existing <c>Users</c> table.
/// Column shapes of the pre-existing properties reproduce the model snapshot exactly
/// (NVARCHAR(MAX), no length tightening) so the migration only ADDS columns.
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(u => u.FullName).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnType("nvarchar(max)").IsRequired();

        // MFA &amp; brute-force guard (spec §2/§3.1): additive, defaulted, backward compatible.
        builder.Property(u => u.TwoFactorEnabled).HasColumnType("bit").HasDefaultValue(false);
        builder.Property(u => u.AccessFailedCount).HasColumnType("int").HasDefaultValue(0);
        builder.Property(u => u.LockoutEnd).HasColumnType("datetimeoffset");
        builder.Property(u => u.AuthenticatorKey).HasColumnType("nvarchar(max)");

        builder.HasMany(u => u.RecoveryCodes)
            .WithOne(c => c.User)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
