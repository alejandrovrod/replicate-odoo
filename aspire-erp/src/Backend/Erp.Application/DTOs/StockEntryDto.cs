using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>One line of a stock voucher as returned by the API.</summary>
public sealed record StockEntryLineDto(
    Guid ItemId,
    string ItemCode,
    string ItemName,
    decimal Qty,
    decimal? Rate,
    int LineNumber,
    // Batch-and-serial Fase 3: batch + serials moved (null/absent for untracked items and
    // for vouchers posted by flows without tracking input, e.g. manufacturing).
    Guid? BatchId = null,
    string? BatchNo = null,
    IReadOnlyList<string>? SerialNumbers = null);

/// <summary>One Kardex row produced by a posted voucher.</summary>
public sealed record StockLedgerEntryDto(
    Guid Id,
    Guid ItemId,
    Guid WarehouseId,
    DateOnly PostingDate,
    decimal QtyChange,
    decimal ValuationRate,
    decimal Amount,
    // Batch-and-serial Fase 3: batch provenance (null for untracked movements).
    Guid? BatchId = null);

/// <summary>One General Ledger line produced by a posted voucher (account included for readability).</summary>
public sealed record GLEntryDto(
    long Id,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    DateOnly PostingDate,
    decimal Debit,
    decimal Credit,
    string VoucherType,
    string VoucherNo,
    string? Remarks);

/// <summary>Stock voucher summary returned by GET /api/v1/stockentries.</summary>
public sealed record StockEntryDto(
    Guid Id,
    Guid CompanyId,
    StockEntryType EntryType,
    DateOnly PostingDate,
    string VoucherNo,
    Guid WarehouseId,
    Guid? TargetWarehouseId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<StockEntryLineDto> Lines);

/// <summary>
/// Full result of POST /api/v1/stockentries: the voucher, its Kardex rows and its balanced
/// General Ledger lines - everything needed to prove Tasks 3.2/3.3 in the API response itself.
/// </summary>
public sealed record StockEntryPostingDto(
    StockEntryDto Entry,
    IReadOnlyList<StockLedgerEntryDto> LedgerEntries,
    IReadOnlyList<GLEntryDto> GlEntries,
    decimal TotalDebit,
    decimal TotalCredit);
