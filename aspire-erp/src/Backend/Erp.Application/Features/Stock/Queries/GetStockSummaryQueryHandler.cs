using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Stock.Queries;

/// <summary>Assembles <see cref="GetStockSummaryQuery"/> from <see cref="IStockRepository"/>.</summary>
public sealed class GetStockSummaryQueryHandler : IQueryHandler<GetStockSummaryQuery, StockSummaryDto>
{
    private readonly IStockRepository _stock;

    public GetStockSummaryQueryHandler(IStockRepository stock)
    {
        _stock = stock;
    }

    public async Task<StockSummaryDto> HandleAsync(
        GetStockSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        var summary = await _stock.GetStockSummaryAsync(query.CompanyId, cancellationToken);
        return new StockSummaryDto(
            summary.TotalSkus,
            summary.ActiveSkus,
            summary.TotalValue,
            summary.WarehouseCount,
            summary.LeafWarehouseCount);
    }
}
