using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Counterpart side of a reconciliation link: either an internal payment voucher or a
/// posted General Ledger voucher (referenced by its <c>VoucherId</c>).
/// Persisted as the enum NAME (nvarchar), matching the <see cref="BankTransactionStatus"/>
/// precedent.
/// </summary>
public enum BankReconciliationCounterpartType
{
    PaymentEntry,
    GLEntry,
}

/// <summary>
/// One allocation slice of a reconciled staging line (invariant BN-04: the slices of a
/// transaction sum to exactly |Deposit - Withdrawal|).
/// </summary>
/// <remarks>
/// Justified deviation from plan.md §1 DDL (which has no link table): multi-voucher
/// allocation needs a home for per-voucher amounts, and <c>GLEntry</c> rows are append-only
/// (Constitution III.2), so the link must live outside both sides. Deleting the transaction
/// deletes its slices; deleting a payment entry is blocked while slices reference it.
/// </remarks>
public sealed class BankReconciliation : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    public Guid BankTransactionId { get; set; }

    public BankTransaction? BankTransaction { get; set; }

    public BankReconciliationCounterpartType CounterpartType { get; set; }

    /// <summary>
    /// <c>PaymentEntry.Id</c> when <see cref="CounterpartType"/> is <c>PaymentEntry</c>;
    /// the <c>GLEntry.VoucherId</c> (source-document id) when it is <c>GLEntry</c>.
    /// </summary>
    public Guid CounterpartId { get; set; }

    /// <summary>Amount of this slice (decimal(18,4), strictly positive).</summary>
    public decimal AllocatedAmount { get; set; }
}
