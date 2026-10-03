using Erp.Domain.Common;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// The manual voucher aggregate root (plan.md §1 "<c>JournalEntry.cs (Manual voucher aggregate
/// root)</c>" / spec.md §1 ubiquitous language: "Multi-line voucher used for manual accounting
/// adjustments, opening balances, bank transfers, write-offs, and inter-company movements").
/// </summary>
/// <remarks>
/// <para><b>Two-step workflow (documented interpretation of plan.md §3).</b> plan §3 sketches ONE
/// command (<c>SubmitJournalEntryCommand(CompanyId, PostingDate, VoucherType, UserRemark, Lines)</c>)
/// that both holds the payload and posts it. tasks.md 2.4 asks for
/// <c>POST /journal-entries</c> <b>plus</b> <c>/{id}/submit</c>, so the module ships the two-step
/// shape instead: CREATE persists the header and its lines in <see cref="JournalEntryStatus.Draft"/>
/// (NO GLEntry rows - the drafts of spec AC-02/AC-03 exist precisely because a draft can hold
/// lines that will later be rejected), and SUBMIT re-runs plan §3's canonical validation
/// verbatim before appending the ledger rows. The payload of the create command keeps plan §3's
/// field set (CompanyId, PostingDate, Type, UserRemark, Lines); the validation logic lives in
/// <see cref="EnsureBalanced"/> + <see cref="JournalEntryValidator"/> and the submit handler calls
/// it inside ONE transaction.
/// </para>
/// <para><b>Lifecycle</b> (only two legal arcs - every other move throws
/// <c>invalid_status_transition</c>, mapped to 409):
/// <c>Draft --Submit()--&gt; Submitted --Cancel()--&gt; Cancelled</c>.</para>
/// <para><b>Append-only.</b> This aggregate NEVER mutates a GLEntry row (Constitution III.2):
/// submit APPENDS rows, cancel APPENDS compensating rows and only flips this header's status
/// (Constitution III.3). The header/lines are ordinary transactional rows - like PurchaseOrder -
/// so the table is NOT temporal and NOT append-only.</para>
/// </remarks>
public class JournalEntry : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Company whose books this voucher writes (the COA the lines must belong to).</summary>
    public Guid CompanyId { get; set; }

    /// <summary>
    /// Gapless voucher number (Constitution III.4): prefix <b>JV</b> per spec AC-07's literal
    /// <c>JV-2026-0081</c>, i.e. <c>JV-YYYY-NNNNN</c>. Assigned inside the CREATION transaction by
    /// the same SELECT MAX ... WITH (UPDLOCK, HOLDLOCK) generator the purchase vouchers use, so a
    /// Draft already carries its number (ERPNext naming series on save) and a rollback consumes
    /// none.
    /// </summary>
    public string VoucherNo { get; set; } = string.Empty;

    /// <summary>Accounting date of the voucher; also the year basis of the JV sequence.</summary>
    public DateOnly PostingDate { get; set; }

    /// <summary>Economic nature of the voucher (spec §1: adjustments, opening balances, ...).</summary>
    public JournalEntryType Type { get; set; } = JournalEntryType.Standard;

    /// <summary>Workflow state - see the class remarks for the legal machine.</summary>
    public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Draft;

    /// <summary>Free-text audit remark; copied to GLEntry.Remarks when the voucher is submitted.</summary>
    public string? UserRemark { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Optimistic concurrency token (SQL Server <c>rowversion</c> - the same store-generated token
    /// PurchaseOrder/StockEntry/Account use): the submit/cancel transitions are read-modify-write,
    /// so EF puts the original value in the UPDATE ... WHERE clause and a concurrent transition
    /// between the load and the save throws <c>DbUpdateConcurrencyException</c>, which the
    /// repository translates to <see cref="ConcurrencyConflictException"/> (<c>concurrency_conflict</c>,
    /// 409) instead of silently losing one of the two writes.
    /// Store-generated: never set from code.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    /// <summary>The voucher's lines - part of the aggregate (delete the header, cascade the lines).</summary>
    public ICollection<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();

    /// <summary>Sum of every line's Debit (decimal(18,4) domain values, pre-rounding).</summary>
    public decimal TotalDebit
    {
        get
        {
            var total = 0m;
            foreach (var line in Lines)
            {
                total += line.Debit;
            }

            return total;
        }
    }

    /// <summary>Sum of every line's Credit (decimal(18,4) domain values, pre-rounding).</summary>
    public decimal TotalCredit
    {
        get
        {
            var total = 0m;
            foreach (var line in Lines)
            {
                total += line.Credit;
            }

            return total;
        }
    }

    /// <summary>
    /// Draft -&gt; Submitted (spec AC-01 "status becomes Submitted"). The caller (the submit
    /// handler) runs plan §3's validation FIRST - imbalance, freeze and group account - so a
    /// rejected submission never reaches this method and never writes a GLEntry row (AC-02).
    /// </summary>
    /// <exception cref="JournalValidationException">The voucher is not a Draft (<c>invalid_status_transition</c>).</exception>
    public void Submit()
    {
        if (Status != JournalEntryStatus.Draft)
        {
            throw new JournalValidationException(
                JournalErrorCodes.InvalidStatusTransition,
                $"Only a Draft journal entry can be submitted; voucher '{VoucherNo}' is '{Status}'.");
        }

        Status = JournalEntryStatus.Submitted;
    }

    /// <summary>
    /// Submitted -&gt; Cancelled (spec AC-07 "the original voucher status transitions to Cancelled").
    /// Cancelling appends compensating reversal rows - it never edits or deletes the originals
    /// (Constitution III.2/III.3), and the reversal carries this voucher's ORIGINAL PostingDate, so
    /// the period lock of spec AC-04 must be re-checked by the caller against that date before any
    /// write: a frozen original period blocks the cancellation too.
    /// </summary>
    /// <exception cref="JournalValidationException">The voucher is not Submitted (<c>invalid_status_transition</c>).</exception>
    public void Cancel()
    {
        if (Status != JournalEntryStatus.Submitted)
        {
            throw new JournalValidationException(
                JournalErrorCodes.InvalidStatusTransition,
                $"Only a Submitted journal entry can be cancelled; voucher '{VoucherNo}' is '{Status}'.");
        }

        Status = JournalEntryStatus.Cancelled;
    }

    /// <summary>
    /// Constitution III.1 / plan.md §3 (canonical logic, verbatim): the voucher may reach the
    /// ledger only when <c>Math.Abs(totalDebit - totalCredit) &lt;= 0.0001m</c>.
    /// </summary>
    /// <remarks>
    /// Runs on the AGGREGATE's lines (the two-step workflow's Draft rows), before any GLEntry row
    /// exists - that ordering is what gives spec AC-02 "zero records are written to GLEntry".
    /// The submit handler then re-checks the built GLEntry list with
    /// <see cref="Services.DoubleEntryGuard"/> before SaveChanges, exactly like the stock and
    /// buying posting engines.
    /// </remarks>
    /// <exception cref="DoubleEntryImbalanceException">|debits - credits| &gt; 0.0001.</exception>
    public void EnsureBalanced()
    {
        var totalDebit = TotalDebit;
        var totalCredit = TotalCredit;

        if (Math.Abs(totalDebit - totalCredit) > 0.0001m)
        {
            throw new DoubleEntryImbalanceException(totalDebit, totalCredit);
        }
    }
}

/// <summary>
/// One debit/credit line of a <see cref="JournalEntry"/>. The voucher-side twin of
/// <see cref="GLEntry"/>: same money columns and same dimensions (Party*, CostCenter), but this
/// row IS editable while the voucher is a Draft - the immutable copy is written to GLEntry on
/// submit (Constitution III.2 applies to the ledger, not to the draft).
/// </summary>
public class JournalEntryLine
{
    public Guid Id { get; set; }

    public Guid JournalEntryId { get; set; }

    public JournalEntry? JournalEntry { get; set; }

    /// <summary>1-based line number inside the voucher.</summary>
    public int LineNumber { get; set; }

    /// <summary>Target account (FK Restrict, same as GLEntry): ledger rows must survive drafts.</summary>
    public Guid AccountId { get; set; }

    public Account? Account { get; set; }

    /// <summary>Debit amount (decimal(18,4), CHECK &gt;= 0 per Constitution IV.3).</summary>
    public decimal Debit { get; set; }

    /// <summary>Credit amount (decimal(18,4), CHECK &gt;= 0 per Constitution IV.3).</summary>
    public decimal Credit { get; set; }

    /// <summary>Counterparty role (e.g. "Supplier"), mirrored onto GLEntry.PartyType on submit.</summary>
    public string? PartyType { get; set; }

    /// <summary>Primary key of the party row referenced by <see cref="PartyType"/>.</summary>
    public Guid? PartyId { get; set; }

    /// <summary>Cost Center dimension, mirrored onto GLEntry.CostCenterId on submit.</summary>
    public Guid? CostCenterId { get; set; }
}
