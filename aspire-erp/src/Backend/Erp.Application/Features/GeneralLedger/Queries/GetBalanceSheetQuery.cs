using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.GeneralLedger.Queries;

/// <summary>
/// Builds the balance sheet of one company as of a date - the read behind
/// <c>GET /api/v1/FinancialReports/balance-sheet</c> (tasks.md 2.5: "Assets = Liabilities +
/// Equity").
/// </summary>
/// <remarks>
/// Only the Asset, Liability and Equity roots are projected: Income and Expense belong to the
/// profit &amp; loss, so an open period legitimately reports <c>Balanced == false</c> by exactly
/// the period's unclosed net profit. <see cref="AsOfDate"/> is inclusive, like the trial balance.
/// </remarks>
/// <param name="CompanyId">Company that owns the ledger (REQUIRED by the endpoint).</param>
/// <param name="AsOfDate">Cutoff date of the statement (REQUIRED by the endpoint).</param>
public sealed record GetBalanceSheetQuery(Guid CompanyId, DateOnly AsOfDate)
    : IQuery<BalanceSheetReportDto>;
