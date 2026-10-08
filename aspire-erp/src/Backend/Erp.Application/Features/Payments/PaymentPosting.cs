using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Payments;

/// <summary>
/// Shared mechanics of the payment posting pipeline (spec R-12): counterparty account
/// resolution and the construction of the append-only GLEntry rows for BOTH transitions.
/// </summary>
/// <remarks>
/// Kept out of the handlers so submit and cancel build rows from ONE implementation - a
/// reversal must be the exact mirror of what was posted, and two copies of the mapping would
/// drift. Pure assembly (Constitution I.2 rules are applied by the callers); no I/O besides
/// the account lookups in <see cref="LoadSettlementAccountsAsync"/>.
/// </remarks>
public static class PaymentPosting
{
    /// <summary>
    /// <c>GLEntry.VoucherType</c> identifier of this pipeline - PascalCase like the established
    /// values ("StockEntry", "JournalEntry"), so the vocabulary column stays consistent.
    /// </summary>
    public const string VoucherType = "PaymentEntry";

    /// <summary>
    /// Compare-and-swap pre-check for the client token carried by the status-transition bodies.
    /// A missing token skips the fast check (the repository still guards the load/save race).
    /// </summary>
    /// <exception cref="ConcurrencyConflictException">The supplied token is stale.</exception>
    public static void EnsureRowVersion(PaymentEntry payment, byte[]? expected)
    {
        if (expected is null)
        {
            return;
        }

        if (payment.RowVersion is null || !payment.RowVersion.AsSpan().SequenceEqual(expected))
        {
            throw new ConcurrencyConflictException(nameof(PaymentEntry), payment.Id);
        }
    }

    /// <summary>
    /// Resolves the two settlement accounts: the voucher's bank GL account and the
    /// counterparty leaf (customer receivable / supplier payable). Fails with
    /// <c>invalid_counterparty_account</c> when the counterparty link does not resolve to
    /// exactly one active leaf of the company.
    /// </summary>
    public static async Task<(Account BankAccount, Account CounterpartyAccount)> LoadSettlementAccountsAsync(
        IAccountRepository accounts,
        BankAccount bankProfile,
        Account counterpartyLeaf,
        CancellationToken cancellationToken)
    {
        var bankGl = await accounts.GetByIdAsync(bankProfile.GLAccountId, cancellationToken)
            ?? throw new BankingValidationException(
                BankingErrorCodes.BankGlAccountNotFound,
                $"Bank GL account '{bankProfile.GLAccountId}' was not found in this tenant.");

        if (bankGl.CompanyId != bankProfile.CompanyId || !bankGl.IsActive || bankGl.IsGroup)
        {
            throw new BankingValidationException(
                BankingErrorCodes.BankGlAccountNotFound,
                $"Bank GL account '{bankProfile.GLAccountId}' is not a postable (active leaf) account "
                + "of this company.");
        }

        return (bankGl, counterpartyLeaf);
    }

    /// <summary>
    /// Resolves the counterparty receivable/payable leaf for a party: the party-level default
    /// account first, falling back to the company-level GL code default (decision D3), resolved
    /// to exactly one active leaf of the company.
    /// </summary>
    /// <exception cref="BankingValidationException">
    /// <c>invalid_counterparty_account</c> when neither link resolves.
    /// </exception>
    public static async Task<Account> ResolveCounterpartyLeafAsync(
        IAccountRepository accounts,
        Guid companyId,
        Guid? partyAccountId,
        string? companyAccountCode,
        string role,
        CancellationToken cancellationToken)
    {
        if (partyAccountId.HasValue)
        {
            var partyAccount = await accounts.GetByIdAsync(partyAccountId.Value, cancellationToken);
            if (partyAccount is not null
                && partyAccount.CompanyId == companyId
                && partyAccount.IsActive
                && !partyAccount.IsGroup)
            {
                return partyAccount;
            }
        }

        if (!string.IsNullOrWhiteSpace(companyAccountCode))
        {
            var matches = await accounts.FindActiveLeafByCodeAsync(companyId, companyAccountCode, cancellationToken);
            if (matches.Count == 1)
            {
                return matches[0];
            }
        }

        throw new BankingValidationException(
            BankingErrorCodes.InvalidCounterpartyAccount,
            $"No postable {role} account resolves for this payment (party default "
            + "and company default both missing or ambiguous).");
    }

    /// <summary>
    /// Builds the two GLEntry rows of one transition (spec R-12 invariant PE-01):
    /// <list type="bullet">
    /// <item><paramref name="isReversal"/> = false (submit): Receive = Dr bank / Cr receivable,
    /// Pay = Dr payable / Cr bank, <c>IsCancelled = false</c>;</item>
    /// <item><paramref name="isReversal"/> = true (cancel): the SAME pair with Debit and Credit
    /// SWAPPED (Constitution III.3 compensating reversal, spec PE-05), same VoucherId/VoucherNo
    /// so the voucher nets to 0.0000, <c>IsCancelled = true</c>.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Both kinds keep the voucher's ORIGINAL PaymentDate and are single-currency snapshots
    /// (amounts book 1:1, AccountCurrency from each line's account - the JournalPosting note).
    /// </remarks>
    public static List<GLEntry> BuildLedgerLines(
        PaymentEntry payment,
        Account bankGlAccount,
        Account counterpartyAccount,
        bool isReversal)
    {
        // Settlement direction: money IN debits the bank, money OUT credits it.
        var bankIsDebit = payment.PaymentType == PaymentType.Receive;
        if (isReversal)
        {
            bankIsDebit = !bankIsDebit;
        }

        var amountCC = payment.PaidAmount * payment.SettlementExchangeRate;
        var amountFC = payment.PaidAmount;

        var bankLine = NewLine(payment, bankGlAccount, 
            bankIsDebit ? amountCC : 0m, bankIsDebit ? 0m : amountCC,
            bankIsDebit ? amountFC : 0m, bankIsDebit ? 0m : amountFC, isReversal);
            
        var counterpartyLine = NewLine(payment, counterpartyAccount, 
            bankIsDebit ? 0m : amountCC, bankIsDebit ? amountCC : 0m, 
            bankIsDebit ? 0m : amountFC, bankIsDebit ? amountFC : 0m, isReversal);

        return new List<GLEntry> { bankLine, counterpartyLine };
    }

    private static GLEntry NewLine(PaymentEntry payment, Account account, decimal debit, decimal credit, decimal debitFC, decimal creditFC, bool isReversal)
        => new()
        {
            CompanyId = payment.CompanyId,
            PostingDate = payment.PaymentDate,
            AccountId = account.Id,

            // Navigation kept populated so a follow-up read can show account code + name
            // without a second round trip (EF fixes up the FK from the reference anyway).
            Account = account,
            Debit = debit,
            Credit = credit,

            DebitInAccountCurrency = debitFC,
            CreditInAccountCurrency = creditFC,
            AccountCurrency = account.Currency?.Code ?? "USD",

            VoucherType = VoucherType,
            VoucherNo = payment.VoucherNo,
            VoucherId = payment.Id,

            PartyType = payment.PartyType.ToString(),
            PartyId = payment.PartyId,
            CostCenterId = null,

            IsCancelled = isReversal,
            Remarks = isReversal
                ? $"Reversal of {VoucherType} {payment.VoucherNo} (cancelled)"
                : $"Payment {payment.VoucherNo} ({payment.PaymentType})",

            // CreatedAt stays on the column default (SYSDATETIMEOFFSET), so reversal rows
            // sort after the originals they cancel.
        };
}
