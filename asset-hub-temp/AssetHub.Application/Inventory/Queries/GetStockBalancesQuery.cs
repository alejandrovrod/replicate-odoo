using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Inventory.Queries;

public class StockBalanceDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid CatalogItemId { get; set; }
    public string CatalogItemCode { get; set; } = string.Empty;
    public string CatalogItemName { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal AverageUnitCost { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class GetStockBalancesQuery : IRequest<List<StockBalanceDto>>
{
    public Guid? WarehouseId { get; set; }
    public Guid? CatalogItemId { get; set; }
}

public class GetStockBalancesQueryHandler : IRequestHandler<GetStockBalancesQuery, List<StockBalanceDto>>
{
    private readonly ITenantDbContext _dbContext;

    public GetStockBalancesQueryHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<StockBalanceDto>> Handle(GetStockBalancesQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.StockBalances
            .Include(s => s.Warehouse)
            .Include(s => s.CatalogItem!.Translations)
            .AsQueryable();

        if (request.WarehouseId.HasValue)
        {
            query = query.Where(s => s.WarehouseId == request.WarehouseId.Value);
        }

        if (request.CatalogItemId.HasValue)
        {
            query = query.Where(s => s.CatalogItemId == request.CatalogItemId.Value);
        }

        var balances = await query
            .OrderBy(s => s.Warehouse!.Name)
            .ThenBy(s => s.CatalogItem!.Code)
            .ToListAsync(cancellationToken);

        return balances.Select(s => new StockBalanceDto
        {
            WarehouseId = s.WarehouseId,
            WarehouseName = s.Warehouse!.Name,
            CatalogItemId = s.CatalogItemId,
            CatalogItemCode = s.CatalogItem!.Code,
            CatalogItemName = s.CatalogItem!.Translations
                .FirstOrDefault(t => t.Locale == "es")?.Label
                ?? s.CatalogItem!.Translations.FirstOrDefault()?.Label
                ?? s.CatalogItem!.Code,
            QuantityOnHand = s.QuantityOnHand,
            AverageUnitCost = s.AverageUnitCost,
            UpdatedAt = s.UpdatedAt
        }).ToList();
    }
}
