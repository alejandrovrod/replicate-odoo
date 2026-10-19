using Erp.Application.Common;
using Erp.Application.Features.Crm.DTOs;
using System;

namespace Erp.Application.Features.Crm.Commands;

/// <summary>
/// Moves an opportunity to another pipeline stage (Block B, spec CRM-02 - the Kanban
/// drag-drop write path). ClosedWon forces Probability=100, ClosedLost forces 0 with a
/// mandatory catalog loss reason (<c>LossReasonId</c>, free-text <c>LossReason</c> now
/// carries only the additional notes); open stages sync the milestone probability
/// unless an explicit one is supplied. <c>RowVersion</c> is the optional optimistic token:
/// a stale token fails fast with <c>concurrency_conflict</c> (spec CRM-06 groundwork).
/// </summary>
public record AdvanceOpportunityStageCommand(
    Guid OpportunityId,
    Guid CompanyId,
    string ToStage,
    decimal? Probability = null,
    string? LossReason = null,
    byte[]? RowVersion = null,
    Guid? LossReasonId = null
) : ICommand<Result<OpportunityDto>>;
