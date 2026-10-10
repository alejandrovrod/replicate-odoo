using System;
using System.Threading.Tasks;

namespace AssetHub.Application.Interfaces;

public interface IBillingProvider
{
    Task<string> CreateCheckoutSessionAsync(Guid tenantId, string planCode);
}
