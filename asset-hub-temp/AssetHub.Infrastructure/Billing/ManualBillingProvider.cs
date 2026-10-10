using System;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;

namespace AssetHub.Infrastructure.Billing;

public class ManualBillingProvider : IBillingProvider
{
    public Task<string> CreateCheckoutSessionAsync(Guid tenantId, string planCode)
    {
        // En modo manual, no hay portal. Devolvemos una URL de "pendiente de contacto"
        return Task.FromResult("https://assethub.app/contact-sales?tenantId=" + tenantId);
    }
}
