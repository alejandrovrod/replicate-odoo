using System;
using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>
/// Closing voucher detail (plan.md §4, extended per spec §7: <c>FiscalYearId</c> + RowVersion).
/// </summary>
public record PeriodClosingVoucherDto(
    Guid Id,
    Guid CompanyId,
    Guid FiscalYearId,
    string VoucherNo,
    DateOnly PostingDate,
    Guid RetainedEarningsAccountId,
    DocumentStatus DocumentStatus,
    string? Remarks,
    string? IdempotencyKey,
    byte[] RowVersion
)
{
    public static PeriodClosingVoucherDto Build(PeriodClosingVoucher voucher) =>
        new(
            voucher.Id,
            voucher.CompanyId,
            voucher.FiscalYearId,
            voucher.VoucherNo,
            voucher.PostingDate,
            voucher.RetainedEarningsAccountId,
            voucher.DocumentStatus,
            voucher.Remarks,
            voucher.IdempotencyKey,
            voucher.RowVersion);
}

/// <summary>Fiscal year master (plan.md §4).</summary>
public record FiscalYearDto(
    Guid Id,
    Guid CompanyId,
    string YearName,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsClosed,
    DateTimeOffset? ClosedAt,
    byte[] RowVersion
)
{
    public static FiscalYearDto Build(FiscalYear year) =>
        new(
            year.Id,
            year.CompanyId,
            year.YearName,
            year.StartDate,
            year.EndDate,
            year.IsClosed,
            year.ClosedAt,
            year.RowVersion);
}

/// <summary>
/// One read-only preview line: the zeroing posting submit would write for one P&amp;L leaf,
/// plus the leaf balance (plan.md §4 <c>ClosingPreviewLineDto</c>).
/// </summary>
public record ClosingPreviewLineDto(
    Guid AccountId,
    string Code,
    string Name,
    string RootType,
    decimal Debit,
    decimal Credit,
    decimal Balance);

/// <summary>Preview envelope: the exact line set submit would post, plus the net P&amp;L.</summary>
public record ClosingPreviewDto(
    Guid CompanyId,
    Guid FiscalYearId,
    IReadOnlyList<ClosingPreviewLineDto> Lines,
    ClosingPreviewLineDto? RetainedLine,
    decimal Net);
