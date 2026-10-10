using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Assets;
using AssetHub.Domain.Incidents;
using AssetHub.Domain.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Maintenance.Helpers;

public static class PreventivePlanExecutionGuard
{
    public static async Task<(bool CanExecute, string? Reason)> CanExecuteForAssetAsync(
        ITenantDbContext db,
        Asset asset,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // The user requested: "no quiero que me bloquees aunque se haya generado una incidencia o se haya creado un plan de mantenimiento porque el activo en la parte operativa si puede estar operando".
        // Removed active incident and open maintenance order blocking logic.

        return (true, null);
    }
}
