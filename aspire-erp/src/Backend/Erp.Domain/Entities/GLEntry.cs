using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// One line of the immutable General Ledger (.specify/spec.md §3: "The atomic, immutable
/// transaction ledger"; plan.md §2 GLEntry DDL). Every voucher produces at least two rows whose
/// debits and credits sum to exactly 0.0000 (Constitution III.1).
/// </summary>
/// <remarks>
/// INSERT-ONLY at BOTH levels (Constitution III.2): <c>AppDbContext</c> throws
/// <see cref="Exceptions.GLEntryAppendOnlyViolationException"/> on any Modified/Deleted entry, and
/// the <c>trg_GLEntry_AppendOnly</c> INSTEAD OF trigger rejects direct UPDATE/DELETE in SQL Server.
/// Deliberately NOT temporal (IV.2 covers master entities; the ledger is append-only instead).
/// Uses a BIGINT IDENTITY key plus the plan.md §2 CHECK constraints and covering indexes.
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

    /// <summary>
    /// Debit restated in the ACCOUNT's currency (plan.md §2 GLEntry, decimal(18,4) NOT NULL
    /// DEFAULT 0.0000). Phase 2 postings are single-currency, so this mirrors
    /// <see cref="Debit"/> 1:1; multi-currency restatement lands with the FX module (spec AC-05).
    /// </summary>
    public decimal DebitInAccountCurrency { get; set; }

    /// <summary>Credit restated in the account's currency - same single-currency note as
    /// <see cref="DebitInAccountCurrency"/>.</summary>
    public decimal CreditInAccountCurrency { get; set; }

    /// <summary>
    /// ISO 4217 currency of the posted account at posting time (plan.md §2, NVARCHAR(3) NOT NULL
    /// DEFAULT 'USD'). Snapshot, not a live join: the ledger is a forensic record (Constitution IV.2).
    /// </summary>
    public string AccountCurrency { get; set; } = "USD";

    /// <summary>Source document type, e.g. "StockEntry".</summary>
    public string VoucherType { get; set; } = string.Empty;

    /// <summary>Source document number, e.g. "MR-2026-00001" (gapless - Constitution III.4).</summary>
    public string VoucherNo { get; set; } = string.Empty;

    /// <summary>
    /// Primary key of the source document aggregate (plan.md §2, UNIQUEIDENTIFIER NOT NULL):
    /// StockEntry / PurchaseReceipt / PurchaseInvoice Id. The durable link that makes
    /// <see cref="VoucherNo"/> (a human number, renumberable by convention) non-authoritative.
    /// Legacy rows written before this column existed are backfilled by migration
    /// AddGLEntryPostingColumns; unmatched rows carry the Guid.Empty sentinel, never a random GUID.
    /// </summary>
    public Guid VoucherId { get; set; }

    /// <summary>
    /// Party role of the counter-occurrence, e.g. "Supplier" for purchase vouchers (plan.md §2,
    /// NVARCHAR(50) NULL). Null when the voucher has no counterparty dimension.
    /// </summary>
    public string? PartyType { get; set; }

    /// <summary>Primary key of the party row referenced by <see cref="PartyType"/>.</summary>
    public Guid? PartyId { get; set; }

    /// <summary>
    /// Cost Center dimension (plan.md §2). NULL until the Cost Center module posts dimensional
    /// tags; declared now so the physical schema matches plan §2 from the first migration.
    /// </summary>
    public Guid? CostCenterId { get; set; }

    /// <summary>
    /// True when this row IS a compensating reversal of a cancelled voucher (Constitution III.3 -
    /// the reversal law; spec AC-07). RESOLVED SEMANTICS (tasks.md 2.3): the marker travels on the
    /// REVERSAL rows, never on the originals - cancellation appends the counter-lines with this
    /// flag set and leaves every original row byte-identical, because Constitution III.2 forbids
    /// the UPDATE the earlier plan wording implied ("flip this marker at cancellation time").
    /// Query the voucher's rows once to see originals (false) + reversals (true); the voucher
    /// header's Status is the authoritative "is it cancelled?" answer.
    /// </summary>
    public bool IsCancelled { get; set; }

    public string? Remarks { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
