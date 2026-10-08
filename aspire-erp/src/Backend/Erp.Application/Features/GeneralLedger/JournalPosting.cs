using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.GeneralLedger;

/// <summary>
/// Shared mechanics of the Journal Entry posting pipeline (tasks.md 2.3): account resolution and
/// the construction of the append-only GLEntry rows for BOTH transitions of the workflow.
/// </summary>
/// <remarks>
/// Kept out of the handlers so submit and cancel build rows from ONE implementation - a reversal
/// must be the exact mirror of what was posted, and two copies of the mapping would drift.
/// Pure assembly (Constitution I.2 rules are applied by the callers); no I/O besides the account
/// lookups in <see cref="LoadAccountsAsync"/>.
/// </remarks>
internal static class JournalPosting
{
    /// <summary>
    /// <c>GLEntry.VoucherType</c> identifier of this pipeline - PascalCase like the established
    /// values ("StockEntry", "PurchaseReceipt", "PurchaseInvoice"), so the vocabulary column stays
    /// consistent.
    /// </summary>
    public const string VoucherType = "JournalEntry";

    /// <summary>
    /// Resolves every distinct account referenced by the voucher's lines, failing with
    /// <c>account_not_found</c> (naming the id and the line) when a lookup misses. Tenant
    /// isolation is automatic (Constitution II.3).
    /// </summary>
    public static async Task<Dictionary<Guid, Account>> LoadAccountsAsync(
        IAccountRepository accounts,
        JournalEntry entry,
        CancellationToken cancellationToken)
    {
        var byId = new Dictionary<Guid, Account>();
        foreach (var line in entry.Lines)
        {
            if (byId.ContainsKey(line.AccountId))
            {
                continue;
            }

            var account = await accounts.GetByIdAsync(line.AccountId, cancellationToken)
                ?? throw new JournalValidationException(
                    JournalErrorCodes.AccountNotFound,
                    $"Account '{line.AccountId}' referenced by line {line.LineNumber} of voucher "
                    + $"'{entry.VoucherNo}' was not found in this tenant.");

            byId[line.AccountId] = account;
        }

        return byId;
    }

    /// <summary>
    /// Builds the GLEntry rows of one transition:
    /// <list type="bullet">
    /// <item><paramref name="isReversal"/> = false (submit): the voucher lines verbatim,
    /// <c>IsCancelled = false</c>, party/cost-center dimensions carried over, Remarks = the
    /// header's UserRemark;</item>
    /// <item><paramref name="isReversal"/> = true (cancel): the SAME lines with Debit and Credit
    /// SWAPPED (Constitution III.3 compensating reversal / spec AC-07), same VoucherId/VoucherNo
    /// so the voucher's net balance returns to 0.0000, <c>IsCancelled = true</c> and Remarks
    /// pointing back at the cancelled voucher.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Both kinds keep the voucher's ORIGINAL PostingDate (AC-04 blocks a cancellation when that
    /// period is frozen) and both are single-currency snapshots: DebitInAccountCurrency/Credit
    /// mirror Debit/Credit and AccountCurrency comes from the account (same note as GLEntry).
    /// </remarks>
    public static List<GLEntry> BuildLedgerLines(
        JournalEntry entry,
        IReadOnlyDictionary<Guid, Account> accountsById,
        bool isReversal)
    {
        var glLines = new List<GLEntry>(entry.Lines.Count);

        foreach (var line in entry.Lines.OrderBy(l => l.LineNumber))
        {
            var account = accountsById[line.AccountId];
            var debit = isReversal ? line.Credit : line.Debit;
            var credit = isReversal ? line.Debit : line.Credit;

            glLines.Add(new GLEntry
            {
                CompanyId = entry.CompanyId,
                PostingDate = entry.PostingDate,
                AccountId = account.Id,

                // Navigation kept populated so a follow-up read can show account code + name
                // without a second round trip (EF fixes up the FK from the reference anyway).
                Account = account,
                Debit = debit,
                Credit = credit,

                // plan.md §2 account-currency pair: single-currency postings book the ledger
                // amount 1:1 and snapshot the account currency (FX restatement = spec AC-05, later).
                DebitInAccountCurrency = debit,
                CreditInAccountCurrency = credit,
                AccountCurrency = account.Currency?.Code ?? "USD",

                VoucherType = VoucherType,
                VoucherNo = entry.VoucherNo,
                VoucherId = entry.Id,

                PartyType = line.PartyType,
                PartyId = line.PartyId,
                CostCenterId = line.CostCenterId,

                // Submit rows are ordinary postings; reversal rows are the ones carrying the
                // cancellation marker (decision documented on GLEntry.IsCancelled - the ORIGINAL
                // rows are never touched, Constitution III.2).
                IsCancelled = isReversal,
                Remarks = isReversal
                    ? $"Reversal of {VoucherType} {entry.VoucherNo} (cancelled)"
                    : entry.UserRemark,

                // CreatedAt stays on the column default (SYSDATETIMEOFFSET), so reversal rows
                // sort after the originals they cancel.
            });
        }

        return glLines;
    }

    /// <summary>
    /// Compare-and-swap pre-check for the OPTIONAL client token carried by the status-transition
    /// bodies. Discovery note: no pre-existing command takes a RowVersion (the buying submit
    /// endpoint relies on the store-generated token alone), so this is additive - when the client
    /// omits the token, <c>JournalRepository.UpdateAsync</c> still guards the load/save race.
    /// </summary>
    /// <exception cref="ConcurrencyConflictException">The supplied token is stale.</exception>
    public static void EnsureRowVersion(JournalEntry entry, byte[]? expected)
    {
        if (expected is null)
        {
            return;
        }

        if (entry.RowVersion is null || !entry.RowVersion.AsSpan().SequenceEqual(expected))
        {
            throw new ConcurrencyConflictException(nameof(JournalEntry), entry.Id);
        }
    }
}
