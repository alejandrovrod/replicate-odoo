using System;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;

namespace AssetHub.Infrastructure.Catalogs;

public class DummyCatalogUsageChecker : ICatalogUsageChecker
{
    public Task<int> GetUsageCountAsync(Guid catalogItemId)
    {
        // Mock: hasta que implementemos Assets e Incidents, asumimos 0 usos.
        return Task.FromResult(0);
    }
}
