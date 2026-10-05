using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Crm.Commands;

/// <summary>
/// Creates a formal sales order from a ClosedWon opportunity (spec CRM-02 1-click creation).
/// A sales order needs LINES (item/qty/rate) while an opportunity carries only an amount, so
/// the caller supplies the single line explicitly - the UI prefills quantity 1 at the
/// opportunity amount - and the order is created through the EXISTING
/// <c>CreateSalesOrderCommand</c> (server-side totals, gapless SO number, zero catalog policy
/// invented here).
/// </summary>
/// <param name="CompanyId">Company that owns the opportunity.</param>
/// <param name="OpportunityId">ClosedWon opportunity to convert.</param>
/// <param name="ItemId">Catalog item for the single order line (chosen by the user).</param>
/// <param name="Quantity">Line quantity (must be &gt; 0).</param>
/// <param name="Rate">Line unit rate (the UI prefills the opportunity amount).</param>
/// <param name="TransactionDate">Order date (defaults to today).</param>
/// <param name="DeliveryDate">Promised shipment date (defaults to today + 30 days, the
/// ConvertLead closing-date precedent).</param>
/// <param name="CreatedByUserId">Audit author for the linkage note (optional - omitted when
/// neither it nor the deal's assignee is known).</param>
public sealed record CreateOpportunitySalesOrderCommand(
    Guid CompanyId,
    Guid OpportunityId,
    Guid ItemId,
    decimal Quantity,
    decimal Rate,
    DateOnly? TransactionDate = null,
    DateOnly? DeliveryDate = null,
    Guid? CreatedByUserId = null) : ICommand<Result<SalesOrderDto>>;
