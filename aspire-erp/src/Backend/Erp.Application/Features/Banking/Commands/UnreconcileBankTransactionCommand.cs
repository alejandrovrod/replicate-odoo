using Erp.Application.Common;

namespace Erp.Application.Features.Banking.Commands;

/// <summary>
/// Reverts a reconciliation (task 6.4, scenario BN-06): the staging line and its linked
/// payment vouchers go back to Unreconciled with NULL clearance and zero allocation.
/// </summary>
/// <param name="CompanyId">Company that owns the transaction.</param>
/// <param name="BankTransactionId">Reconciled staging line to reopen.</param>
/// <param name="RowVersion">Optional optimistic token - a stale token fails fast.</param>
public sealed record UnreconcileBankTransactionCommand(
    Guid CompanyId,
    Guid BankTransactionId,
    byte[]? RowVersion = null) : ICommand<Result<bool>>;
