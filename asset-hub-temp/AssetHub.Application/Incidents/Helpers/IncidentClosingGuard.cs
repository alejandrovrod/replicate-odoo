using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Incidents;
using AssetHub.Domain.Maintenance;
using AssetHub.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Incidents.Helpers;

public static class IncidentClosingGuard
{
    public static async Task<bool> CanCloseAsync(
        ITenantDbContext db,
        Guid incidentId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var incident = await db.Incidents
            .Include(i => i.MaintenanceOrders)
            .Include(i => i.WorkTasks)
            .FirstOrDefaultAsync(i => i.Id == incidentId && i.TenantId == tenantId, cancellationToken);

        if (incident == null)
            return false;

        var hasActiveOrders = incident.MaintenanceOrders
            .Any(o => !o.IsDeleted && MaintenanceOrderStates.ActiveStates.Contains(o.State));

        if (hasActiveOrders)
            return false;

        var hasOpenTasks = incident.WorkTasks
            .Any(t => !t.IsDeleted && !WorkTaskStates.TerminalStates.Contains(t.State));

        if (hasOpenTasks)
            return false;

        return true;
    }
}
