using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Lifecycle of a staging bank line: imported unreconciled (BN-01), optionally matched by the
/// heuristic rules engine (Block B, task 6.3), reconciled by the reconciliation tool (task 6.4),
/// or excluded by the operator. Persisted as the enum NAME (nvarchar), matching the
/// <see cref="StockEntryType"/> precedent.
/// </summary>
public enum BankTransactionStatus
{
    Unreconciled,
    Matched,
    Reconciled,
    Excluded,
}

/// <summary>
/// Raw statement line in isolated staging (plan.md §1 DDL). Importing a line creates ONLY this
/// row - zero <c>GLEntry</c> rows (invariant BN-01). Deposit / withdrawal sides are mutually
/// exclusive (invariant BN-02, enforced by <see cref="BankTransactionValidator"/>).
/// </summary>
public sealed class BankTransaction : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid BankAccountId { get; set; }

    public BankAccount? BankAccount { get; set; }

    /// <summary>Value date of the statement line.</summary>
    public DateOnly TransactionDate { get; set; }

    /// <summary>Money in (decimal(18,4), &gt;= 0; zero when this line is a withdrawal).</summary>
    public decimal Deposit { get; set; }

    /// <summary>Money out (decimal(18,4), &gt;= 0; zero when this line is a deposit).</summary>
    public decimal Withdrawal { get; set; }

    /// <summary>ISO 4217 currency (3 chars, default 'USD', plan.md §1).</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>Statement narrative (max 500 chars, plan.md §1).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Bank reference, if the file carries one.</summary>
    public string? ReferenceNumber { get; set; }

    /// <summary>
    /// Bank external transaction id (OFX FITID): the de-duplication key for idempotent re-imports
    /// (scenario BN-05). Null when the format carries no stable id (plain CSV).
    /// </summary>
    public string? TransactionId { get; set; }

    public BankTransactionStatus Status { get; set; } = BankTransactionStatus.Unreconciled;

    /// <summary>Amount already reconciled against vouchers (decimal(18,4), default 0).</summary>
    public decimal AllocatedAmount { get; set; }

    /// <summary>
    /// Date the line cleared. Stamped by reconciliation (invariant BN-03, Block B); null until then.
    /// </summary>
    public DateOnly? ClearanceDate { get; set; }

    /// <summary>
    /// Suggestion pre-populated by the rules engine (task 6.3, scenario BN-02): the counterparty
    /// role of the first matching rule, if it names one. Null until a rule stamps it.
    /// </summary>
    public string? SuggestedPartyType { get; set; }

    /// <summary>Suggested counterparty row id from the first matching rule, if it names one.</summary>
    public Guid? SuggestedPartyId { get; set; }

    /// <summary>Suggested clearing / expense account from the first matching rule, if it names one.</summary>
    public Guid? SuggestedAccountId { get; set; }

    /// <summary>
    /// Optimistic concurrency token (SQL Server <c>rowversion</c> - scenario BN-07): two clerks
    /// matching the same line concurrently resolve to exactly one winner. Store-generated.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
