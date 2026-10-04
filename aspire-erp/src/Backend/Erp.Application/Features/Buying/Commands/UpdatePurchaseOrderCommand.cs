using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>One ordered item of an <see cref="UpdatePurchaseOrderCommand"/>.</summary>
public sealed record UpdatePurchaseOrderItem(Guid ItemId, decimal Quantity, decimal Rate);

/// <summary>
/// Updates a Draft purchase order (Task 4.2). Fails if the order is not in Draft state.
/// Rewrites all lines.
/// </summary>
public sealed record UpdatePurchaseOrderCommand(
    Guid CompanyId,
    Guid PurchaseOrderId,
    Guid SupplierId,
    DateOnly TransactionDate,
    DateOnly ScheduleDate,
    IReadOnlyList<UpdatePurchaseOrderItem>? Items = null) : ICommand<Result<PurchaseOrderDto>>;
