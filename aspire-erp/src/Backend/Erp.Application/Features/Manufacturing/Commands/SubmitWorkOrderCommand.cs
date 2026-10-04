using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Manufacturing.Commands;

/// <summary>
/// Advances one Draft work order to Submitted (Task 9.3 workflow). Submission verifies the
/// referenced BOM exists, is active and is the default recipe - the task acceptance gate. Only
/// Draft orders submit; any other state fails with <c>invalid_status_transition</c> (409).
/// No stock and no GL impact.
/// </summary>
public sealed record SubmitWorkOrderCommand(
    Guid CompanyId,
    Guid WorkOrderId) : ICommand<Result<WorkOrderDto>>;
