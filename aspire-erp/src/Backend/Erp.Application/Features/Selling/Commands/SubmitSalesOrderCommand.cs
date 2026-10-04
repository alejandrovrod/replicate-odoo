using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Selling.Commands;

/// <summary>
/// Submits one Draft sales order (Task 5.2): runs the spec SL-02 credit gate FIRST and, on
/// success, moves the order Draft -&gt; Submitted. No stock and no GL impact - the delivery note
/// (Task 5.2b) is what relieves inventory.
/// </summary>
/// <remarks>
/// A credit breach is reported as the stable <c>credit_limit_exceeded</c> failure, which the API
/// maps to 409: the request conflicts with the customer's credit state, not with the payload.
/// </remarks>
public sealed record SubmitSalesOrderCommand(
    Guid CompanyId,
    Guid SalesOrderId) : ICommand<Result<SalesOrderDto>>;
