using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>One ordered line of a <see cref="CreatePurchaseOrderCommand"/>.</summary>
public sealed record CreatePurchaseOrderLine(Guid ItemId, decimal Qty, decimal Rate);

/// <summary>
/// Creates one purchase order in Draft with its gapless PO-YYYY-NNNNN voucher (Task 4.1).
/// No stock and no GL impact - the workflow starts here: Draft -&gt; Ordered -&gt; Received -&gt; Billed.
/// </summary>
public sealed record CreatePurchaseOrderCommand(
    Guid CompanyId,
    Guid SupplierId,
    DateOnly? PostingDate = null,
    IReadOnlyList<CreatePurchaseOrderLine>? Lines = null) : ICommand<Result<PurchaseOrderDto>>;
