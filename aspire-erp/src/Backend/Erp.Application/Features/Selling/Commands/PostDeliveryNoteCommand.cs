using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Services;

namespace Erp.Application.Features.Selling.Commands;

/// <summary>One line of a <see cref="PostDeliveryNoteCommand"/>: ships <paramref name="Qty"/> against ONE order line.</summary>
public sealed record PostDeliveryNoteLine(Guid SalesOrderItemId, Guid ItemId, decimal Qty);

/// <summary>
/// Creates AND posts one delivery note (Task 5.2b / Amendment A1) in a single transaction:
/// Kardex rows at FIFO cost, balanced General Ledger lines (Dr Cost of Goods Sold / Cr warehouse
/// stock account), the gapless DN voucher and the linked order's delivery update. There is no
/// draft state: the command IS the posting.
/// </summary>
public sealed record PostDeliveryNoteCommand(
    Guid CompanyId,
    Guid SalesOrderId,
    Guid WarehouseId,
    DateOnly? PostingDate = null,
    IReadOnlyList<PostDeliveryNoteLine>? Lines = null) : ICommand<Result<DeliveryNotePostingDto>>;
