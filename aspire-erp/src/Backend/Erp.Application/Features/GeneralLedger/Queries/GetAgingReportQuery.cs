using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.GeneralLedger.Queries;

/// <summary>
/// Builds the AR/AP aging report of one company as of a date - the read behind
/// <c>GET /api/v1/FinancialReports/aging</c> and tasks.md 7.1 (ERPNext buckets
/// 0-30 / 31-60 / 61-90 / 90+ days).
/// </summary>
/// <remarks>
/// Only posted, open invoices count (Unpaid/PartiallyPaid with Outstanding &gt; 0 and
/// <c>PostingDate &lt;= ReportDate</c>); drafts, paid and cancelled documents never appear.
/// Age is measured against the due date (the ERPNext <c>ageing_based_on = Due Date</c>
/// semantics); negative ages land in the NotDue bucket instead of 0-30.
/// </remarks>
/// <param name="CompanyId">Company that owns the invoices.</param>
/// <param name="ReportDate">Aging anchor date (inclusive).</param>
public sealed record GetAgingReportQuery(Guid CompanyId, DateOnly ReportDate)
    : IQuery<AgingReportDto>;
