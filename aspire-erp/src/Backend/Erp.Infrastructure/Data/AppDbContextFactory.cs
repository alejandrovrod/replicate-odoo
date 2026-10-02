using Erp.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Erp.Infrastructure.Data;

/// <summary>
/// Design-time factory so `dotnet ef --project src/Backend/Erp.Infrastructure` can build the model
/// without booting Erp.Api (Task 1.4). Options only - no Api configuration involved.
/// </summary>
/// <remarks>
/// Connection resolution: the <c>ConnectionStrings__erp-db</c> environment variable (Aspire naming)
/// wins; the fallback placeholder is enough for `migrations add`, which never opens a connection.
/// `database update` against the real container must set the env variable (no credentials are
/// committed here).
/// </remarks>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__erp-db")
            ?? "Server=127.0.0.1,1433;Database=erp-db;User Id=sa;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options, new TenantProvider());
    }
}
