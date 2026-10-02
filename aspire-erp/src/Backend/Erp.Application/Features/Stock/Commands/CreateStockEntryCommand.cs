using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;

namespace Erp.Application.Features.Stock.Commands;

/// <summary>One line of a stock entry command (qty in the item's Base UOM).</summary>
public sealed record CreateStockEntryLine(Guid ItemId, decimal Qty, decimal? Rate = null);

/// <summary>
/// Creates AND POSTS one stock voucher atomically (Task 3.2): there is no draft lifecycle, so the
/// command IS the posting - FIFO valuation, Kardex rows, balanced General Ledger lines and the
/// gapless voucher number all happen inside one transaction (decision D4).
/// </summary>
public sealed record CreateStockEntryCommand(
    Guid CompanyId,
    StockEntryType EntryType,
    Guid WarehouseId,
    Guid? TargetWarehouseId = null,
    DateOnly? PostingDate = null,
    IReadOnlyList<CreateStockEntryLine>? Lines = null) : ICommand<Result<StockEntryPostingDto>>;
