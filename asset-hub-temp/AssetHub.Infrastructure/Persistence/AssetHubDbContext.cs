using Microsoft.EntityFrameworkCore;

namespace AssetHub.Infrastructure.Persistence;

public class AssetHubDbContext(DbContextOptions<AssetHubDbContext> options) : DbContext(options)
{
}
