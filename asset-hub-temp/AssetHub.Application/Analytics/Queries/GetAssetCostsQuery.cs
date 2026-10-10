using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Maintenance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Analytics.Queries;

public class GetAssetCostsQuery : IRequest<decimal>
{
    public Guid AssetId { get; set; }
    public bool IncludeSubtree { get; set; }
}

public class GetAssetCostsQueryHandler : IRequestHandler<GetAssetCostsQuery, decimal>
{
    private readonly ITenantDbContext _db;

    public GetAssetCostsQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<decimal> Handle(GetAssetCostsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.MaintenanceOrders.AsQueryable();

        if (request.IncludeSubtree)
        {
            var subtreeIds = await _db.AssetHierarchies
                .Where(h => h.AncestorId == request.AssetId)
                .Select(h => h.DescendantId)
                .ToListAsync(cancellationToken);
            
            // Incluye el propio AssetId (ya que el hierarchy podría no incluirlo a sí mismo dependiendo de la convención de la jerarquía)
            subtreeIds.Add(request.AssetId);
            
            query = query.Where(o => subtreeIds.Contains(o.AssetId));
        }
        else
        {
            query = query.Where(o => o.AssetId == request.AssetId);
        }

        // RN-16.2: Costo acumulado = Σ(LaborCost + parts) de órdenes done/verified
        var cost = await query
            .Where(o => o.State == MaintenanceOrderStates.Done || o.State == MaintenanceOrderStates.Verified)
            .SumAsync(o => o.LaborCost + o.Parts.Sum(p => p.Quantity * p.UnitCost), cancellationToken);

        return cost;
    }
}
