using Erp.Application.Common;

namespace Erp.Application.Features.Stock.Commands;

/// <summary>
/// Cancels a posted stock voucher (spec AC-07 / Task 3.7). Cancellation is append-only:
/// it flags the voucher as IsCancelled = true and appends compensating (negative) rows to the
/// Kardex and General Ledger.
/// </summary>
public sealed record CancelStockEntryCommand(Guid CompanyId, Guid StockEntryId) : ICommand<Result<bool>>;
