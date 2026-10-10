using System.Threading;
using System.Threading.Tasks;
using AssetHub.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Interfaces;

public interface IPlatformDbContext
{
    DbSet<Tenant> Tenants { get; set; }
    DbSet<Plan> Plans { get; set; }
    DbSet<Subscription> Subscriptions { get; set; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
