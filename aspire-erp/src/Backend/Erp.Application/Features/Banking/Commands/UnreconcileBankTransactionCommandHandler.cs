using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Banking.Commands;

/// <summary>
/// Executes <see cref="UnreconcileBankTransactionCommand"/> (scenario BN-06): deletes the
/// transaction's links and reverts Status / AllocatedAmount / ClearanceDate on the line AND
/// on every linked <see cref="PaymentEntry"/> (ClearanceDate back to NULL, Status back to
/// Unreconciled) - inside ONE transaction.
/// </summary>
/// <remarks>
/// GL vouchers are NEVER touched by the un-reconcile either (they never were by the
/// reconcile: GLEntry is append-only, Constitution III.2). A line that is not Reconciled
/// fails with <c>invalid_status_transition</c>; unknown / foreign lines fail with
/// <c>bank_transaction_not_found</c> - both with zero writes.
/// </remarks>
public sealed class UnreconcileBankTransactionCommandHandler
    : ICommandHandler<UnreconcileBankTransactionCommand, Result<bool>>
{
    private readonly IBankRepository _bank;

    public UnreconcileBankTransactionCommandHandler(IBankRepository bank)
    {
        _bank = bank;
    }

    public async Task<Result<bool>> HandleAsync(
        UnreconcileBankTransactionCommand command,
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

                if (transaction.Status != BankTransactionStatus.Reconciled)
                {
                    throw new BankingValidationException(
                        BankingErrorCodes.InvalidStatusTransition,
                        $"Bank transaction '{transaction.Id}' is {transaction.Status} and cannot be "
                        + "un-reconciled (only Reconciled lines can).");
                }

                var links = await _bank.GetReconciliationsByTransactionAsync(transaction.Id, token);

                var entries = new Dictionary<Guid, PaymentEntry>();
                foreach (var link in links)
                {
                    if (link.CounterpartType != BankReconciliationCounterpartType.PaymentEntry)
                    {
                        continue;
                    }

                    if (!entries.ContainsKey(link.CounterpartId))
                    {
                        var entry = await _bank.GetPaymentEntryByIdAsync(link.CounterpartId, token);
                        if (entry is not null)
                        {
                            entries[link.CounterpartId] = entry;
                        }
                    }
                }

                transaction.Status = BankTransactionStatus.Unreconciled;
                transaction.AllocatedAmount = 0m;
                transaction.ClearanceDate = null;

                foreach (var entry in entries.Values)
                {
                    // BN-06: clearance back to NULL; the accounting date is never touched
                    // (it never was - see the reconcile handler's BN-03 note).
                    entry.Status = PaymentStatus.Unreconciled;
                    entry.ClearanceDate = null;
                }

                // Header saves BEFORE the link delete (same precedent as the reconcile
                // handler): a save race then leaves the links intact.
                await _bank.UpdateTransactionAsync(transaction, token);
                foreach (var entry in entries.Values)
                {
                    await _bank.UpdatePaymentEntryAsync(entry, token);
                }

                await _bank.RemoveReconciliationsAsync(transaction.Id, token);

                return Result<bool>.Success(true);
            }, cancellationToken);
        }
        catch (BankingValidationException ex)
        {
            return Result<bool>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<bool>.Failure(ex.Code, ex.Message);
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
