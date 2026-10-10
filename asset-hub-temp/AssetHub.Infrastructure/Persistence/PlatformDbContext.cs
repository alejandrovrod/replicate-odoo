using AssetHub.Application.Interfaces;
using AssetHub.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Infrastructure.Persistence;

public class PlatformDbContext : DbContext, IPlatformDbContext
{
    public PlatformDbContext(DbContextOptions<PlatformDbContext> options) : base(options)
    {
    }

    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<Plan> Plans { get; set; } = null!;
    public DbSet<Subscription> Subscriptions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("platform");

        modelBuilder.Entity<Tenant>(b =>
        {
            b.HasKey(t => t.Id);
            b.HasIndex(t => t.Slug).IsUnique();
        });

        modelBuilder.Entity<Plan>(b =>
        {
            b.HasKey(p => p.Id);
            b.HasIndex(p => p.Code).IsUnique();
        });

        modelBuilder.Entity<Subscription>(b =>
        {
            b.HasKey(s => s.Id);
            b.HasIndex(s => s.TenantId);
        });

        base.OnModelCreating(modelBuilder);
    }
}
