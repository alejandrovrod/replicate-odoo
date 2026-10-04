using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Banking.Commands;

/// <summary>
/// Executes <see cref="ReconcileBankTransactionCommand"/>: validates every line, then - inside
/// ONE transaction - persists the <see cref="BankReconciliation"/> links, marks the staging
/// line Reconciled and stamps ClearanceDate on both sides (task 6.4, BN-03/BN-04/BN-07).
/// </summary>
/// <remarks>
/// <para><b>Zero-write validation.</b> Every rejection (unknown ids, foreign company, bad
/// status, amount mismatch, over-consumption, stale token) returns BEFORE any mutation or
/// save, so a failed reconcile persists nothing.</para>
/// <para><b>BN-04.</b> Sum(lines.Amount) must equal |Deposit - Withdrawal| EXACTLY
/// (<c>reconciliation_amount_mismatch</c>); an off-by-cent fails with zero writes.</para>
/// <para><b>PaymentEntry path.</b> The entry must exist and belong to the company
/// (<c>payment_entry_not_found</c>). Consumption is cumulative: consumed = sum of ALL
/// existing links for that entry; consumed + newly requested must not exceed PaidAmount
/// (<c>over_allocation</c>, the task 6.1 code). Only Status + ClearanceDate are touched -
/// PaymentDate (the voucher's accounting date, i.e. the spec's PostingDate) is NEVER
/// written (BN-03).</para>
/// <para><b>GLEntry path.</b> The referenced voucher must have at least one GL row with
/// <c>VoucherId == GlVoucherId</c> owned by the company (<c>gl_voucher_not_found</c>),
/// read through the existing <see cref="IGLEntryRepository"/> page query - ZERO GL writes
/// on this path (GLEntry is append-only, Constitution III.2). EXACT CHECK: the line Amount
/// must equal the voucher's total Debit, i.e. SUM(Debit) over the voucher's rows (which
/// equals SUM(Credit) by the balance invariant III.1), else <c>gl_amount_mismatch</c>.
/// GL rows are never mutated.</para>
/// <para><b>BN-07.</b> A stale client token fails fast with <c>concurrency_conflict</c>
/// (compare-and-swap, the CancelPurchaseInvoice precedent); a load/save race surfaces the
/// same code via the repository's RowVersion translation.</para>
/// </remarks>
public sealed class ReconcileBankTransactionCommandHandler
    : ICommandHandler<ReconcileBankTransactionCommand, Result<ReconciliationSummary>>
{
    private readonly IBankRepository _bank;
    private readonly IGLEntryRepository _ledger;

    public ReconcileBankTransactionCommandHandler(IBankRepository bank, IGLEntryRepository ledger)
    {
        _bank = bank;
        _ledger = ledger;
    }

    public async Task<Result<ReconciliationSummary>> HandleAsync(
        ReconcileBankTransactionCommand command,
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
                        + "reconciled (only Unreconciled or Matched lines can).");
                }

                if (command.Lines.Count == 0)
                {
                    throw new BankingValidationException(
                        BankingErrorCodes.ReconciliationAmountMismatch,
                        "Reconciliation requires at least one allocation line.");
                }

                foreach (var line in command.Lines)
                {
                    ValidateLineShape(line);
                }

                var expected = Math.Abs(transaction.Deposit - transaction.Withdrawal);
                var requested = command.Lines.Sum(l => l.Amount);
                if (requested != expected)
                {
                    throw new BankingValidationException(
                        BankingErrorCodes.ReconciliationAmountMismatch,
                        $"Allocation total {requested:0.####} does not equal the transaction amount "
                        + $"{expected:0.####} (difference must be $0.00).");
                }

                // Resolve + validate every counterpart BEFORE mutating anything (zero-write rule).
                var entries = new Dictionary<Guid, PaymentEntry>();
                var requestedByEntry = new Dictionary<Guid, decimal>();
                foreach (var line in command.Lines)
                {
                    if (line.PaymentEntryId is { } entryId)
                    {
                        if (!entries.TryGetValue(entryId, out var entry))
                        {
                            entry = await _bank.GetPaymentEntryByIdAsync(entryId, token);
                            if (entry is null || entry.CompanyId != command.CompanyId)
                            {
                                throw new BankingValidationException(
                                    BankingErrorCodes.PaymentEntryNotFound,
                                    $"Payment entry '{entryId}' was not found in company "
                                    + $"'{command.CompanyId}'.");
                            }

                            entries[entryId] = entry;
                        }

                        requestedByEntry[entryId] =
                            requestedByEntry.GetValueOrDefault(entryId) + line.Amount;
                    }
                    else
                    {
                        await EnsureGlVoucherAsync(command.CompanyId, line.GlVoucherId!.Value, line.Amount, token);
                    }
                }

                foreach (var (entryId, amount) in requestedByEntry)
                {
                    var entry = entries[entryId];
                    var consumed = await _bank.GetConsumedAmountForPaymentEntryAsync(entryId, token);
                    if (consumed + amount > entry.PaidAmount)
                    {
                        throw new BankingValidationException(
                            BankingErrorCodes.OverAllocation,
                            $"Cannot allocate {amount:0.####} from payment entry '{entryId}': already "
                            + $"consumed {consumed:0.####} against a paid amount of "
                            + $"{entry.PaidAmount:0.####}.",
                            amount,
                            entry.PaidAmount,
                            consumed);
                    }
                }

                // All checks passed: mutate + persist atomically.
                var links = new List<BankReconciliation>(command.Lines.Count);
                foreach (var line in command.Lines)
                {
                    if (line.PaymentEntryId is { } entryId)
                    {
                        links.Add(new BankReconciliation
                        {
                            Id = Guid.NewGuid(),
                            TenantId = transaction.TenantId,
                            BankTransactionId = transaction.Id,
                            CounterpartType = BankReconciliationCounterpartType.PaymentEntry,
                            CounterpartId = entryId,
                            AllocatedAmount = line.Amount,
                        });
                    }
                    else
                    {
                        links.Add(new BankReconciliation
                        {
                            Id = Guid.NewGuid(),
                            TenantId = transaction.TenantId,
                            BankTransactionId = transaction.Id,
                            CounterpartType = BankReconciliationCounterpartType.GLEntry,
                            CounterpartId = line.GlVoucherId!.Value,
                            AllocatedAmount = line.Amount,
                        });
                    }
                }

                transaction.Status = BankTransactionStatus.Reconciled;
                transaction.AllocatedAmount = requested;
                transaction.ClearanceDate = transaction.TransactionDate;

                foreach (var entry in entries.Values)
                {
                    // BN-03: stamp clearance only. PaymentDate (the accounting date) is NEVER
                    // touched, preserving the fiscal audit trail.
                    entry.Status = PaymentStatus.Reconciled;
                    entry.ClearanceDate = transaction.TransactionDate;
                }

                // Header saves BEFORE the link appends (the CancelPurchaseInvoice precedent):
                // a save race then leaves no orphan links behind.
                await _bank.UpdateTransactionAsync(transaction, token);
                foreach (var entry in entries.Values)
                {
                    await _bank.UpdatePaymentEntryAsync(entry, token);
                }

                await _bank.AddReconciliationsAsync(links, token);

                return Result<ReconciliationSummary>.Success(new ReconciliationSummary(
                    transaction.Id,
                    requested,
                    transaction.TransactionDate,
                    links.Select(l => new ReconciledLine(
                        l.Id, l.CounterpartType.ToString(), l.CounterpartId, l.AllocatedAmount))
                        .ToList()));
            }, cancellationToken);
        }
        catch (BankingValidationException ex)
        {
            return Result<ReconciliationSummary>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<ReconciliationSummary>.Failure(ex.Code, ex.Message);
        }
    }

    private static void ValidateLineShape(ReconciliationLine line)
    {
        var hasEntry = line.PaymentEntryId is not null;
        var hasGl = line.GlVoucherId is not null;

        if (hasEntry == hasGl)
        {
            throw new BankingValidationException(
                BankingErrorCodes.InvalidReconciliationAmount,
                "Each reconciliation line must reference exactly one counterpart "
                + "(PaymentEntryId xor GlVoucherId).");
        }

        if (line.Amount <= 0m)
        {
            throw new BankingValidationException(
                BankingErrorCodes.InvalidReconciliationAmount,
                $"Reconciliation line amount must be positive (received {line.Amount:0.####}).");
        }
    }

    private async Task EnsureGlVoucherAsync(
        Guid companyId,
        Guid glVoucherId,
        decimal lineAmount,
        CancellationToken cancellationToken)
    {
        // Read-only by contract: GetGeneralLedgerPageAsync never writes (IGLEntryRepository
        // exposes no write path, Constitution III.2).
        var page = await _ledger.GetGeneralLedgerPageAsync(
            companyId,
            new GeneralLedgerFilter(null, glVoucherId, null, null, null),
            int.MaxValue,
            cancellationToken);

        if (page.Items.Count == 0)
        {
            throw new BankingValidationException(
                BankingErrorCodes.GlVoucherNotFound,
                $"GL voucher '{glVoucherId}' was not found in company '{companyId}'.");
        }

        // Company-owned by construction: the page query is already scoped to companyId, and the
        // tenant filter is automatic - a foreign voucher simply yields zero rows above.
        var voucherTotal = page.Items.Sum(r => r.Debit);
        if (lineAmount != voucherTotal)
        {
            throw new BankingValidationException(
                BankingErrorCodes.GlAmountMismatch,
                $"Reconciliation line amount {lineAmount:0.####} does not equal GL voucher "
                + $"'{glVoucherId}' total {voucherTotal:0.####}.");
        }
    }

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
