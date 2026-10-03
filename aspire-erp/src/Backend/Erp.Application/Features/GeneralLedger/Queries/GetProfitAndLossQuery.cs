using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.GeneralLedger.Queries;

/// <summary>
/// Builds the profit &amp; loss of one company for a period - the read behind
/// <c>GET /api/v1/FinancialReports/profit-and-loss</c> (tasks.md 2.5: "Revenue - COGS - Expenses =
/// Net Profit").
/// </summary>
/// <remarks>
/// Both bounds are inclusive and the period is REQUIRED by the endpoint: unlike a balance sheet
/// (a snapshot), a P&amp;L without dates has no meaning.
/// </remarks>
/// <param name="CompanyId">Company that owns the ledger (REQUIRED by the endpoint).</param>
/// <param name="From">Inclusive first posting date of the period (REQUIRED by the endpoint).</param>
/// <param name="To">Inclusive last posting date of the period (REQUIRED by the endpoint).</param>
public sealed record GetProfitAndLossQuery(Guid CompanyId, DateOnly From, DateOnly To)
    : IQuery<ProfitAndLossReportDto>;
