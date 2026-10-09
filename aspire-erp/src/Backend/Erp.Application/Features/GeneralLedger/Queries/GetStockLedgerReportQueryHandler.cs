using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.GeneralLedger.Queries;

/// <summary>
/// Assembles <see cref="GetStockLedgerReportQuery"/> from <see cref="IStockLedgerReportRepository"/>:
/// one opening row per (item, warehouse) pair with pre-period activity, then the period
/// movements with running balances. Pure read path: the injected contract exposes no
/// writes, so the report cannot mutate the Kardex it measures.
/// </summary>
public sealed class GetStockLedgerReportQueryHandler
    : IQueryHandler<GetStockLedgerReportQuery, StockLedgerReportDto>
{
    private readonly IStockLedgerReportRepository _ledger;

    public GetStockLedgerReportQueryHandler(IStockLedgerReportRepository ledger)
    {
        _ledger = ledger;
    }

    public async Task<StockLedgerReportDto> HandleAsync(
        GetStockLedgerReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var entries = await _ledger.GetLedgerEntriesByCompanyAsync(
            query.CompanyId,
            query.From,
            query.To,
            query.ItemId,
            query.WarehouseId,
            query.Take,
            cancellationToken);

        var rows = new List<StockLedgerRowDto>(entries.Count + 8);
        decimal totalIn = 0m;
        decimal totalOut = 0m;
        decimal totalValueChange = 0m;

        // Entries arrive chronological (PostingDate, CreatedAt, Id); group runs keep the
        // running balances per (item, warehouse) exact, mirroring the ERPNext opening-row
        // logic (single item+warehouse scope) generalized to multi-pair pages.
        var groups = new Dictionary<(Guid ItemId, Guid WarehouseId), (decimal Qty, decimal Value, bool Opened)>();

        foreach (var entry in entries)
        {
            var key = (entry.ItemId, entry.WarehouseId);
            if (!groups.TryGetValue(key, out var state))
            {
                var opening = await _ledger.GetOpeningBalanceAsync(
                    entry.ItemId, entry.WarehouseId, query.From, cancellationToken);

                var averageRate = opening.Qty != 0m ? opening.Value / opening.Qty : 0m;
                rows.Add(new StockLedgerRowDto(
                    entry.ItemId,
                    entry.Item?.ItemCode ?? entry.ItemId.ToString(),
                    entry.Item?.ItemName ?? string.Empty,
                    entry.WarehouseId,
                    entry.Warehouse?.WarehouseCode ?? entry.WarehouseId.ToString(),
                    query.From,
                    "Opening",
                    string.Empty,
                    0m,
                    0m,
                    opening.Qty,
                    averageRate,
                    opening.Value,
                    0m,
                    true));

                state = (opening.Qty, opening.Value, true);
            }

            var inQty = entry.QtyChange > 0m ? entry.QtyChange : 0m;
            var outQty = entry.QtyChange < 0m ? entry.QtyChange : 0m;
            var balanceQty = state.Qty + entry.QtyChange;
            var balanceValue = state.Value + entry.Amount;

            rows.Add(new StockLedgerRowDto(
                entry.ItemId,
                entry.Item?.ItemCode ?? entry.ItemId.ToString(),
                entry.Item?.ItemName ?? string.Empty,
                entry.WarehouseId,
                entry.Warehouse?.WarehouseCode ?? entry.WarehouseId.ToString(),
                entry.PostingDate,
                entry.VoucherType,
                entry.VoucherNo,
                inQty,
                outQty,
                balanceQty,
                entry.ValuationRate,
                balanceValue,
                entry.Amount,
                false));

            groups[key] = (balanceQty, balanceValue, true);
            totalIn += inQty;
            totalOut += outQty;
            totalValueChange += entry.Amount;
        }

        return new StockLedgerReportDto(
            query.CompanyId, query.From, query.To, rows, totalIn, totalOut, totalValueChange);
    }
}
