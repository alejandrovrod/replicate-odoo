using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure C# field and posting rules for the Journal Entry pipeline (tasks.md 2.3). No EF Core, no
/// NuGet packages - Constitution Article I.2 keeps Erp.Domain dependency-free, so every rule here
/// is unit-tested without a database (mirrors <see cref="PurchaseValidator"/>/<see cref="StockEntryValidator"/>).
/// </summary>
/// <remarks>
/// Two-phase validation is deliberate (see the workflow remarks on <see cref="JournalEntry"/>):
/// STRUCTURE rules (<see cref="EnsureHasLines"/>, <see cref="EnsureValidLine"/>) run at CREATE so
/// an obviously broken payload never becomes a row, while the ACCOUNTING rules
/// (<see cref="EnsurePostableAccounts"/> + <see cref="JournalEntry.EnsureBalanced"/>) run at
/// SUBMIT, because spec AC-02/AC-03 are phrased on a Draft voucher that is then rejected at
/// submission time - and because an account may change (or a period may be frozen) between the
/// draft and the posting.
/// </remarks>
public static class JournalEntryValidator
{
    /// <summary>A journal entry must carry at least one line.</summary>
    /// <exception cref="JournalValidationException">The line list is empty (<c>no_lines</c>).</exception>
    public static void EnsureHasLines(IReadOnlyCollection<object>? lines)
    {
        if (lines is null || lines.Count == 0)
        {
            throw new JournalValidationException(
                JournalErrorCodes.NoLines,
                "A journal entry must contain at least one line.");
        }
    }

    /// <summary>
    /// Line money rules: both sides &gt;= 0 (Constitution IV.3 / the plan §2 CHECK constraints)
    /// and at least one side non-zero - a line that posts nothing is a client error, not a
    /// silent no-op.
    /// </summary>
    /// <exception cref="JournalValidationException">An amount is negative or both sides are zero (<c>invalid_amount</c>).</exception>
    public static void EnsureValidLine(decimal debit, decimal credit)
    {
        if (debit < 0m || credit < 0m)
        {
            throw new JournalValidationException(
                JournalErrorCodes.InvalidAmount,
                $"Journal line amounts must not be negative (received debit {debit:0.####}, "
                + $"credit {credit:0.####}).");
        }

        if (debit == 0m && credit == 0m)
        {
            throw new JournalValidationException(
                JournalErrorCodes.InvalidAmount,
                "A journal line must post a debit or a credit (both amounts are zero).");
        }
    }

    /// <summary>
    /// Constitution III.3 / spec AC-03 (plan.md §3 "Ensure no line posts to a group account"):
    /// every account a line targets must exist, belong to the company whose books are being
    /// written, be ACTIVE and be a LEAF (non-group) posting account.
    /// </summary>
    /// <remarks>
    /// Identical rule set to <c>PurchasePostingService.EnsurePostable</c> (the buying engine), with
    /// the difference mandated by spec AC-03: the GROUP case surfaces as
    /// <see cref="InvalidPostingAccountException"/> (<c>posting_to_group_account_prohibited</c>)
    /// rather than the generic <c>invalid_gl_account</c> - the spec names that condition
    /// explicitly, so it gets its own exception and its own code.
    /// </remarks>
    /// <param name="accounts">Distinct accounts resolved from the voucher lines (never null: the
    /// handler resolves each line's account first and fails with <c>account_not_found</c> listing
    /// the offending id when a lookup misses).</param>
    /// <param name="companyId">Company that owns the voucher.</param>
    /// <exception cref="JournalValidationException">Inactive or foreign account (<c>invalid_gl_account</c>).</exception>
    /// <exception cref="InvalidPostingAccountException">The account is a group account.</exception>
    public static void EnsurePostableAccounts(
        IReadOnlyCollection<Account> accounts,
        Guid companyId)
    {
        foreach (var account in accounts)
        {
            if (account.IsGroup)
            {
                // spec AC-03: group = folder, never a posting target.
                throw new InvalidPostingAccountException(account.AccountCode, account.AccountName);
            }

            if (!account.IsActive)
            {
                throw new JournalValidationException(
                    JournalErrorCodes.InvalidGlAccount,
                    $"Account '{account.AccountCode}' is inactive and cannot receive "
                    + "General Ledger postings.");
            }

            if (account.CompanyId != companyId)
            {
                throw new JournalValidationException(
                    JournalErrorCodes.InvalidGlAccount,
                    $"Account '{account.AccountCode}' belongs to another company and cannot be "
                    + "posted from this company.");
            }
        }
    }
}
