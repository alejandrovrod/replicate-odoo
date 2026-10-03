using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.GeneralLedger.Queries;

/// <summary>
/// Builds the trial balance of one company as of a date - the read behind
/// <c>GET /api/v1/FinancialReports/trial-balance</c> and the acceptance of tasks.md 2.5
/// ("Trial balance reports zero discrepancy").
/// </summary>
/// <remarks>
/// <see cref="AsOfDate"/> is inclusive (<c>PostingDate &lt;= AsOfDate</c>, plan.md §4); accounts
/// without movement on or before that date are absent from the result instead of showing a zero
/// row, so the statement lists exactly the accounts the ledger touched.
/// </remarks>
/// <param name="CompanyId">Company that owns the ledger (REQUIRED by the endpoint).</param>
/// <param name="AsOfDate">Cutoff date of the statement (REQUIRED by the endpoint).</param>
public sealed record GetTrialBalanceQuery(Guid CompanyId, DateOnly AsOfDate)
    : IQuery<TrialBalanceReportDto>;
