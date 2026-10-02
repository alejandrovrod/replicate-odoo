using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// One line of the immutable General Ledger (.specify/spec.md §3: "The atomic, immutable
/// transaction ledger"; plan.md §3.8). Every voucher produces at least two rows whose debits and
/// credits sum to exactly 0.0000 (Constitution III.1).
/// </summary>
/// <remarks>
/// INSERT-ONLY at BOTH levels (Constitution III.2): <c>AppDbContext</c> throws
/// <see cref="Exceptions.GLEntryAppendOnlyViolationException"/> on any Modified/Deleted entry, and
/// the <c>trg_GLEntry_AppendOnly</c> INSTEAD OF trigger rejects direct UPDATE/DELETE in SQL Server.
/// Deliberately NOT temporal (IV.2 covers master entities; the ledger is append-only instead).
/// Uses BIGINT IDENTITY key + the plan.md §3.8 CHECK constraints and covering index.
/// </remarks>
public class GLEntry : ITenantEntity
{
    public long Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public DateOnly PostingDate { get; set; }

    /// <summary>Posting (leaf) account this line hits. Must exist, be active and non-group (III.3).</summary>
    public Guid AccountId { get; set; }

    public Account? Account { get; set; }

    /// <summary>Debit amount (decimal(18,4), CHECK >= 0 per Constitution IV.3).</summary>
    public decimal Debit { get; set; }

    /// <summary>Credit amount (decimal(18,4), CHECK >= 0 per Constitution IV.3).</summary>
    public decimal Credit { get; set; }

    /// <summary>Source document type, e.g. "StockEntry".</summary>
    public string VoucherType { get; set; } = string.Empty;

    /// <summary>Source document number, e.g. "MR-2026-00001" (gapless - Constitution III.4).</summary>
    public string VoucherNo { get; set; } = string.Empty;

    public string? Remarks { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
