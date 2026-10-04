using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>One ordered item of a <see cref="CreatePurchaseOrderCommand"/>.</summary>
public sealed record CreatePurchaseOrderItem(Guid ItemId, decimal Quantity, decimal Rate);

/// <summary>
/// Creates one purchase order in Draft with its gapless PO-YYYY-NNNNN voucher (Task 4.1).
/// No stock and no GL impact - the workflow starts here: Draft -&gt; Submitted -&gt; PartiallyReceived -&gt; Completed.
/// </summary>
public sealed record CreatePurchaseOrderCommand(
    Guid CompanyId,
    Guid SupplierId,
    DateOnly TransactionDate,
    DateOnly ScheduleDate,
    IReadOnlyList<CreatePurchaseOrderItem>? Items = null) : ICommand<Result<PurchaseOrderDto>>;
