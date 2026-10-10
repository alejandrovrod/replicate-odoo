using System;
using System.Threading.Tasks;

namespace AssetHub.Application.Interfaces;

public interface IEntityTypeUsageChecker
{
    Task<int> GetUsageCountAsync(Guid entityTypeId);
}
