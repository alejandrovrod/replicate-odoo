using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.GeneralLedger.Queries;

/// <summary>
/// Builds the stock ledger (Kardex) report of one company for a period - the read behind
/// <c>GET /api/v1/FinancialReports/stock-ledger</c> and tasks.md 7.1 ("Kardex FIFO").
/// </summary>
/// <remarks>
/// Both bounds are inclusive (<c>From &lt;= PostingDate &lt;= To</c>, the ERPNext
/// <c>from_date</c>/<c>to_date</c> semantics); <c>take</c> caps the movement rows (default
/// 500, max 5000, the general-ledger paging precedent). Opening balances are computed per
/// (item, warehouse) from pre-period rows, so the running balances stay exact even when the
/// window starts mid-history.
/// </remarks>
/// <param name="CompanyId">Company that owns the warehouses.</param>
/// <param name="From">Inclusive first posting date.</param>
/// <param name="To">Inclusive last posting date.</param>
/// <param name="ItemId">Optional single-item filter.</param>
/// <param name="WarehouseId">Optional single-warehouse filter.</param>
/// <param name="Take">Maximum movement rows (opening rows are never truncated).</param>
public sealed record GetStockLedgerReportQuery(
    Guid CompanyId,
    DateOnly From,
    DateOnly To,
    Guid? ItemId = null,
    Guid? WarehouseId = null,
    int Take = 500)
    : IQuery<StockLedgerReportDto>;
