namespace Erp.Domain.Entities;

/// <summary>
/// Lifecycle of a Journal Entry voucher (spec AC-01 "status becomes Submitted" / AC-07 "the
/// original voucher status transitions to Cancelled"). Persisted as the enum NAME (nvarchar(20)),
/// exactly like <see cref="PurchaseOrderStatus"/>.
/// </summary>
/// <remarks>
/// The legal machine is intentionally tiny - only two arcs, both mandated by the spec:
/// <list type="bullet">
/// <item><see cref="Draft"/> -&gt; <see cref="Submitted"/> (AC-01: balanced lines are appended to
/// GLEntry, nothing else changes them);</item>
/// <item><see cref="Submitted"/> -&gt; <see cref="Cancelled"/> (AC-07: compensating reversal rows
/// are appended, the originals stay byte-identical).</item>
/// </list>
/// Every other combination (cancel a Draft, submit twice, resurrect a Cancelled voucher) throws
/// <c>invalid_status_transition</c>, which the API maps to 409 - the same wire value and status
/// the buying workflow uses for the same class of conflict.
/// </remarks>
public enum JournalEntryStatus
{
    /// <summary>Created, editable, NO ledger impact yet - its lines live only in JournalEntryLine.</summary>
    Draft,

    /// <summary>Posted: N balanced rows exist in GLEntry with VoucherId = this voucher.</summary>
    Submitted,

    /// <summary>Cancelled: compensating reversal rows were appended; the voucher can never post again.</summary>
    Cancelled,
}
