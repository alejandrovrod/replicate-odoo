using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Selling.Commands;

/// <summary>One ordered line of a <see cref="CreateSalesOrderCommand"/> (quantity in the item's Base UOM).</summary>
public sealed record CreateSalesOrderLine(Guid ItemId, decimal Quantity, decimal Rate);

/// <summary>
/// Creates one sales order in <c>Draft</c> with its gapless SO-YYYY-NNNNN number (Task 5.2).
/// No stock and no GL impact - the workflow starts here: Draft -&gt; Submitted -&gt;
/// PartiallyDelivered -&gt; Completed. Both dates are REQUIRED (plan.md §1 DATE NOT NULL, no
/// server default), which is why they are not defaulted in this record.
/// </summary>
public sealed record CreateSalesOrderCommand(
    Guid CompanyId,
    Guid CustomerId,
    DateOnly? TransactionDate,
    DateOnly? DeliveryDate,
    IReadOnlyList<CreateSalesOrderLine>? Lines = null) : ICommand<Result<SalesOrderDto>>;
