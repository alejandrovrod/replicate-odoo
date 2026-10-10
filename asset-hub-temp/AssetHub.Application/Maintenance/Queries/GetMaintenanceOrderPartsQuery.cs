using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Maintenance.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Maintenance.Queries;

public record GetMaintenanceOrderPartsQuery(Guid MaintenanceOrderId) : IRequest<List<MaintenanceOrderPartDto>>;

public class GetMaintenanceOrderPartsQueryHandler : IRequestHandler<GetMaintenanceOrderPartsQuery, List<MaintenanceOrderPartDto>>
{
    private readonly ITenantDbContext _db;

    public GetMaintenanceOrderPartsQueryHandler(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<List<MaintenanceOrderPartDto>> Handle(GetMaintenanceOrderPartsQuery request, CancellationToken cancellationToken)
    {
        var parts = await _db.MaintenanceParts
            .AsNoTracking()
            .Include(p => p.CatalogItem)
                .ThenInclude(ci => ci!.Translations)
            .Where(p => p.MaintenanceOrderId == request.MaintenanceOrderId)
            .Select(p => new MaintenanceOrderPartDto
            {
                Id = p.Id,
                CatalogItemId = p.CatalogItemId,
                CatalogItemLabel = p.CatalogItem != null
                    ? p.CatalogItem.Translations
                        .Where(tr => tr.Locale == "es")
                        .Select(tr => tr.Label)
                        .FirstOrDefault()
                    : null,
                Quantity = p.Quantity,
                UnitCost = p.UnitCost
            })
            .ToListAsync(cancellationToken);

        return parts;
    }
}
