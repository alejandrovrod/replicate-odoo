using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.GeneralLedger.Queries;

/// <summary>
/// Page-size limits of the general-ledger report (pinned Task 2.5/2.6 contract). Declared OUTSIDE
/// <see cref="GetGeneralLedgerQuery"/> on purpose: a primary-constructor parameter default may not
/// reference a member of the type it belongs to (the type does not exist yet at that point), so
/// the constants live in their own static home and both the query and the controller default to
/// them instead of repeating the literals.
/// </summary>
public static class GeneralLedgerPaging
{
    /// <summary>Row cap applied when the client does not send <c>take</c> (500).</summary>
    public const int DefaultTake = 500;

    /// <summary>Hard upper bound on <c>take</c> (5000).</summary>
    public const int MaxTake = 5000;
}

/// <summary>
/// Loads the general ledger of one company as a chronological page with the totals of the FULL
/// filtered set - the read behind <c>GET /api/v1/FinancialReports/general-ledger</c> and the
/// pinned Task 2.6 contract the React audit viewer consumes.
/// </summary>
/// <remarks>
/// Every filter is OPTIONAL and they combine with AND (account ∩ voucher ∩ type ∩ date range); an
/// unset filter widens the report instead of narrowing it. Tenant scoping is automatic
/// (Constitution II.3) - the query names a COMPANY, never a tenant.
/// </remarks>
/// <param name="CompanyId">Company that owns the ledger (REQUIRED by the endpoint).</param>
/// <param name="AccountId">Exact posting account, or null for all accounts.</param>
/// <param name="VoucherId">Exact source-document id (voucher drill-down), or null.</param>
/// <param name="VoucherType">Exact source-document type (e.g. "JournalEntry"), or null.</param>
/// <param name="From">Inclusive lower bound on PostingDate, or null.</param>
/// <param name="To">Inclusive upper bound on PostingDate, or null.</param>
/// <param name="Take">
/// Rows to return. Normalized by the handler: 0 or negative falls back to
/// <see cref="GeneralLedgerPaging.DefaultTake"/> and anything above
/// <see cref="GeneralLedgerPaging.MaxTake"/> is capped, so a client can never ask for an
/// unbounded page.
/// </param>
public sealed record GetGeneralLedgerQuery(
    Guid CompanyId,
    Guid? AccountId = null,
    Guid? VoucherId = null,
    string? VoucherType = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Take = GeneralLedgerPaging.DefaultTake) : IQuery<GeneralLedgerReportDto>;
