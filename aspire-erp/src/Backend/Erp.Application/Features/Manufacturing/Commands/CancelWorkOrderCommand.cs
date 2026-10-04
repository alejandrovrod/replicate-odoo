using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Manufacturing.Commands;

/// <summary>
/// Cancels a work order before manufacture begins (Task 9.6, spec MF-05): a Submitted order
/// with no transfer cancels as a pure status transition; an InProcess order (MF-02 transfer
/// posted) additionally reverses its transfer with a compensating WIP -&gt; Stores
/// <c>MaterialTransfer</c> voucher so WIP nets back to zero. Cancelled-from Completed (or any
/// produced quantity) is rejected - FIFO layers are never unpicked.
/// </summary>
public sealed record CancelWorkOrderCommand(
    Guid CompanyId,
    Guid WorkOrderId,
    DateOnly? PostingDate = null) : ICommand<Result<WorkOrderDto>>;
