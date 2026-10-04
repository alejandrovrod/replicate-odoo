using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>
/// Advances one Draft purchase order to Submitted (Task 4.1 workflow). Only Draft orders submit;
/// any other state fails with <c>invalid_status_transition</c> (409). No stock and no GL impact.
/// </summary>
public sealed record SubmitPurchaseOrderCommand(
    Guid CompanyId,
    Guid PurchaseOrderId) : ICommand<Result<PurchaseOrderDto>>;
