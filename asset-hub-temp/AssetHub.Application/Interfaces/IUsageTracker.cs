using System.Threading.Tasks;

namespace AssetHub.Application.Interfaces;

public interface IUsageTracker
{
    Task<int> GetCurrentUsersCountAsync();
    Task<int> GetCurrentAssetsCountAsync();
    Task<long> GetCurrentStorageBytesAsync();
}
