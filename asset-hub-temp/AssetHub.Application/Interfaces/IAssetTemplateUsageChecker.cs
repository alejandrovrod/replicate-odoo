using System;
using System.Threading.Tasks;

namespace AssetHub.Application.Interfaces;

public interface IAssetTemplateUsageChecker
{
    Task<int> GetUsageCountAsync(Guid assetTemplateId);
}
