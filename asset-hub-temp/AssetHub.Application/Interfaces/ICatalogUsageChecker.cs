using System;
using System.Threading.Tasks;

namespace AssetHub.Application.Interfaces;

public interface ICatalogUsageChecker
{
    Task<int> GetUsageCountAsync(Guid catalogItemId);
}
