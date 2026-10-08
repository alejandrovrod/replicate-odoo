using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.GeneralLedger;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Features.Banking.Commands;

/// <summary>
/// Executes <see cref="CreateVoucherFromBankTransactionCommand"/> (task 6.5, scenario BN-04):
/// resolves both ledger accounts, posts a balanced SUBMITTED journal voucher, then links and
/// reconciles the staging line - all inside ONE transaction.
/// </summary>
/// <remarks>
/// <para><b>Zero-write validation (Constitution III.3).</b> Every rejection - unknown ids,
/// foreign company, bad status, amount mismatch, unknown/ambiguous expense code, missing bank
/// GL account, group/inactive account, frozen period, stale token - returns BEFORE any mutation
/// or save, so a failed dialog persists nothing.</para>
/// <para><b>GL reuse, not duplication.</b> The voucher rows are built by
/// <see cref="JournalPosting.BuildLedgerLines"/> (the same method the submit pipeline uses) and
/// guarded by <see cref="DoubleEntryGuard"/> plus <see cref="JournalEntry.EnsureBalanced"/>;
/// the reconcile tail (Status / AllocatedAmount / ClearanceDate + the GLEntry link) mirrors the
/// <see cref="ReconcileBankTransactionCommandHandler"/> link semantics.</para>
/// <para><b>Atomicity.</b> In production both repositories share the scoped
/// <c>AppDbContext</c>, so the outer <c>IBankRepository</c> transaction is ambient for the
/// journal writes too: a failure anywhere rolls back the voucher, the status move and the link
/// together.</para>
/// </remarks>
public sealed class CreateVoucherFromBankTransactionCommandHandler
    : ICommandHandler<CreateVoucherFromBankTransactionCommand, Result<JournalEntryDto>>
{
    private const string VoucherPrefix = "JV";

    private readonly IBankRepository _bank;
    private readonly IAccountRepository _accounts;
    private readonly ICompanyRepository _companies;
    private readonly IJournalRepository _journals;

    public CreateVoucherFromBankTransactionCommandHandler(
        IBankRepository bank,
        IAccountRepository accounts,
        ICompanyRepository companies,
        IJournalRepository journals)
    {
        _bank = bank;
        _accounts = accounts;
        _companies = companies;
        _journals = journals;
    }

    public async Task<Result<JournalEntryDto>> HandleAsync(
        CreateVoucherFromBankTransactionCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _bank.ExecuteInTransactionAsync(async token =>
            {
                var transaction = await _bank.GetTransactionByIdAsync(command.BankTransactionId, token);
                if (transaction is null || transaction.CompanyId != command.CompanyId)
                {
                    throw new BankingValidationException(
                        BankingErrorCodes.BankTransactionNotFound,
                        $"Bank transaction '{command.BankTransactionId}' was not found in company "
                        + $"'{command.CompanyId}'.");
                }

                EnsureRowVersion(transaction.RowVersion, transaction.Id, command.RowVersion);

                if (transaction.Status is not (BankTransactionStatus.Unreconciled or BankTransactionStatus.Matched))
                {
                    throw new BankingValidationException(
                        BankingErrorCodes.InvalidStatusTransition,
                        $"Bank transaction '{transaction.Id}' is {transaction.Status} and cannot be "
                        + "vouchered (only Unreconciled or Matched lines can).");
                }

                var expected = Math.Abs(transaction.Deposit - transaction.Withdrawal);
                if (expected <= 0m)
                {
                    throw new BankingValidationException(
                        BankingErrorCodes.InvalidReconciliationAmount,
                        $"Bank transaction '{transaction.Id}' carries a zero amount and cannot fund "
                        + "a voucher.");
                }

                if (command.Amount.HasValue && command.Amount.Value != expected)
                {
                    throw new BankingValidationException(
                        BankingErrorCodes.ReconciliationAmountMismatch,
                        $"Voucher amount {command.Amount.Value:0.####} does not equal the transaction "
                        + $"amount {expected:0.####} (difference must be $0.00).");
                }

                var amount = command.Amount ?? expected;
                if (amount <= 0m)
                {
                    throw new BankingValidationException(
                        BankingErrorCodes.InvalidReconciliationAmount,
                        $"Voucher amount must be positive (received {amount:0.####}).");
                }

                var bankAccount = await _bank.GetAccountByIdAsync(transaction.BankAccountId, token);
                if (bankAccount is null || bankAccount.CompanyId != command.CompanyId)
                {
                    throw new BankingValidationException(
                        BankingErrorCodes.BankAccountNotFound,
                        $"Bank account '{transaction.BankAccountId}' was not found in company "
                        + $"'{command.CompanyId}'.");
                }

                var company = await _companies.GetByIdAsync(command.CompanyId, token)
                    ?? throw new BankingValidationException(
                        BankingErrorCodes.CompanyNotFound,
                        $"Company '{command.CompanyId}' was not found in this tenant.");

                // AC-04 first data gate, like every other posting engine: a back-dated statement
                // line in a frozen period rejects before any row exists.
                company.EnsurePostingDateUnlocked(transaction.TransactionDate);
                // R-13 FC-04: closed fiscal year rejects the posting too (second half of plan.md §3).
                await _companies.EnsurePostingDateInOpenYearAsync(company.Id, transaction.TransactionDate, token);

                var expenseAccount = await RequireExpenseAccountAsync(
                    command.CompanyId, command.ExpenseAccountCode, token);

                var bankGlAccount = await _accounts.GetByIdAsync(bankAccount.GLAccountId, token)
                    ?? throw new BankingValidationException(
                        BankingErrorCodes.BankGlAccountNotFound,
                        $"Bank account '{bankAccount.AccountNumber}' is wired to GL account "
                        + $"'{bankAccount.GLAccountId}', which was not found in this tenant.");

                // Constitution III.3: both accounts resolved, now sanity-checked BEFORE any write.
                var accountsById = new Dictionary<Guid, Account>
                {
                    [expenseAccount.Id] = expenseAccount,
                    [bankGlAccount.Id] = bankGlAccount,
                };
                JournalEntryValidator.EnsurePostableAccounts(
                    accountsById.Values.ToList(), command.CompanyId);

                var isWithdrawal = transaction.Withdrawal > 0m;
                var entry = new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = command.CompanyId,
                    Status = JournalEntryStatus.Draft,
                    Type = JournalEntryType.Standard,
                    PostingDate = transaction.TransactionDate,
                    UserRemark = string.IsNullOrWhiteSpace(command.Memo)
                        ? $"Quick voucher for bank transaction '{transaction.Description}'."
                        : command.Memo,
                    VoucherNo = await _journals.NextVoucherNumberAsync(
                        command.CompanyId, VoucherPrefix, transaction.TransactionDate.Year, token),
                    CreatedAt = DateTimeOffset.UtcNow,
                    Lines = isWithdrawal
                        ? new List<JournalEntryLine>
                        {
                            VoucherLine(expenseAccount, amount, 0m, 1),
                            VoucherLine(bankGlAccount, 0m, amount, 2),
                        }
                        : new List<JournalEntryLine>
                        {
                            VoucherLine(bankGlAccount, amount, 0m, 1),
                            VoucherLine(expenseAccount, 0m, amount, 2),
                        },
                };

                entry.EnsureBalanced();
                entry.Submit();

                var glLines = JournalPosting.BuildLedgerLines(entry, accountsById, isReversal: false);
                DoubleEntryGuard.EnsureBalanced(glLines);

                // Persist the voucher first, then the reconcile tail (the CancelPurchaseInvoice
                // precedent): a save race then leaves the link out, never an orphan link.
                await _journals.AddAsync(entry, token);
                await _journals.AddGlEntriesAsync(glLines, token);

                transaction.Status = BankTransactionStatus.Reconciled;
                transaction.AllocatedAmount = amount;
                transaction.ClearanceDate = transaction.TransactionDate;
                await _bank.UpdateTransactionAsync(transaction, token);

                await _bank.AddReconciliationsAsync(new List<BankReconciliation>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        TenantId = transaction.TenantId,
                        BankTransactionId = transaction.Id,
                        CounterpartType = BankReconciliationCounterpartType.GLEntry,
                        CounterpartId = entry.Id,
                        AllocatedAmount = amount,
                    },
                }, token);

                return Result<JournalEntryDto>.Success(JournalEntryDto.Build(entry));
            }, cancellationToken);
        }
        catch (BankingValidationException ex)
        {
            return Result<JournalEntryDto>.Failure(ex.Code, ex.Message);
        }
        catch (JournalValidationException ex)
        {
            return Result<JournalEntryDto>.Failure(ex.Code, ex.Message);
        }
        catch (InvalidPostingAccountException ex)
        {
            return Result<JournalEntryDto>.Failure(ex.Code, ex.Message);
        }
        catch (DoubleEntryImbalanceException ex)
        {
            return Result<JournalEntryDto>.Failure(ex.Code, ex.Message);
        }
        catch (FiscalPeriodLockedException ex)
        {
            return Result<JournalEntryDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<JournalEntryDto>.Failure(ex.Code, ex.Message);
        }
    }

    private async Task<Account> RequireExpenseAccountAsync(
        Guid companyId,
        string? accountCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accountCode))
        {
            throw new BankingValidationException(
                BankingErrorCodes.ExpenseAccountNotFound,
                "An expense account code is required to create a quick voucher.");
        }

        var matches = await _accounts.FindActiveLeafByCodeAsync(companyId, accountCode, cancellationToken);

        if (matches.Count == 0)
        {
            throw new BankingValidationException(
                BankingErrorCodes.ExpenseAccountNotFound,
                $"Expense account code '{accountCode}' does not resolve to an ACTIVE LEAF account "
                + $"of company '{companyId}'.");
        }

        if (matches.Count > 1)
        {
            throw new BankingValidationException(
                BankingErrorCodes.ExpenseAccountNotFound,
                $"Expense account code '{accountCode}' is ambiguous: {matches.Count} active leaf "
                + $"accounts share that code in company '{companyId}'.");
        }

        return matches[0];
    }

    private static JournalEntryLine VoucherLine(Account account, decimal debit, decimal credit, int lineNumber) =>
        new()
        {
            Id = Guid.NewGuid(),
            Debit = debit,
            Credit = credit,
            LineNumber = lineNumber,
            AccountId = account.Id,
            Account = account,
        };

    private static void EnsureRowVersion(byte[] current, Guid transactionId, byte[]? expected)
    {
        if (expected is null)
        {
            return;
        }

        if (current is null || !current.AsSpan().SequenceEqual(expected))
        {
            throw new ConcurrencyConflictException(nameof(BankTransaction), transactionId);
        }
    }
}
