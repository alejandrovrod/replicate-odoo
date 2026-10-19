using Erp.Application.Common;
using Erp.Application.Features.Crm.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Erp.Application.Features.Crm.Commands;

/// <summary>
/// Executes <see cref="AdvanceOpportunityStageCommand"/> (Block B, spec CRM-02/CRM-06).
/// Closed deals (Won/Lost) are immutable here - only <c>ReopenOpportunity</c> revives a lost
/// one - and every rejection path writes ZERO rows.
/// </summary>
public sealed class AdvanceOpportunityStageCommandHandler
    : ICommandHandler<AdvanceOpportunityStageCommand, Result<OpportunityDto>>
{
    /// <summary>
    /// Spec §1 milestone probabilities (Value Proposition rides the <c>Proposal</c> stage
    /// constant): the sync target when the caller supplies no explicit probability.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, decimal> StageProbabilities =
        new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            [OpportunityStage.Prospecting] = 10m,
            [OpportunityStage.Qualification] = 25m,
            [OpportunityStage.Proposal] = 50m,
            [OpportunityStage.Negotiation] = 80m,
        };

    private readonly ICrmRepository _crmRepository;
    private readonly ICrmCatalogRepository _catalogs;

    public AdvanceOpportunityStageCommandHandler(
        ICrmRepository crmRepository,
        ICrmCatalogRepository? catalogs = null)
    {
        _crmRepository = crmRepository;
        _catalogs = catalogs;
    }

    public async Task<Result<OpportunityDto>> HandleAsync(
        AdvanceOpportunityStageCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var opportunity = await _crmRepository.GetOpportunityByIdAsync(command.OpportunityId, cancellationToken)
                ?? throw new CRMValidationException(
                    "crm_opportunity_not_found",
                    $"Opportunity '{command.OpportunityId}' not found.");

            if (opportunity.CompanyId != command.CompanyId)
            {
                throw new CRMValidationException(
                    "crm_opportunity_company_mismatch",
                    "Opportunity does not belong to the specified company.");
            }

            // DisbursePayroll EnsureRowVersion precedent: a supplied token that no longer
            // matches fails fast, before any domain work.
            EnsureRowVersion(opportunity, command.RowVersion);

            if (opportunity.Status is OpportunityStatus.Won or OpportunityStatus.Lost or OpportunityStatus.Expired)
            {
                throw new CRMValidationException(
                    CRMErrorCodes.InvalidStatusTransition,
                    $"Opportunity '{opportunity.OpportunityNumber}' is '{opportunity.Status}' and cannot advance; re-open it first.");
            }

            if (!StageProbabilities.ContainsKey(command.ToStage)
                && command.ToStage != OpportunityStage.ClosedWon
                && command.ToStage != OpportunityStage.ClosedLost)
            {
                throw new CRMValidationException(
                    CRMErrorCodes.InvalidOpportunityStage,
                    $"Stage '{command.ToStage}' is not a defined opportunity stage.");
            }

            if (command.ToStage == OpportunityStage.ClosedWon)
            {
                // Entity method is the single rule source: forces Probability=100, clears any
                // loss reason (spec CRM-01). An explicitly supplied probability is overridden.
                opportunity.MarkAsClosedWon();
            }
            else if (command.ToStage == OpportunityStage.ClosedLost)
            {
                // MarkAsClosedLost is the single rule source: a missing catalog reason throws
                // crm_loss_reason_required (spec CRM-02) and forces Probability=0. The legacy
                // free-text LossReason travels as additional notes only.
                var reasonId = await ResolveLossReasonIdAsync(opportunity, command, cancellationToken);
                opportunity.MarkAsClosedLost(reasonId, command.LossReason);
            }
            else
            {
                if (command.Probability.HasValue
                    && (command.Probability.Value < 0 || command.Probability.Value > 100))
                {
                    throw new CRMValidationException(
                        CRMErrorCodes.InvalidProbabilityRange,
                        $"Probability must be between 0 and 100. Received {command.Probability.Value}.");
                }

                var sameStage = string.Equals(command.ToStage, opportunity.Stage, StringComparison.Ordinal);
                // Entity method owns the plain move (probability sync + loss clearing); the
                // range gate above stays handler-side so the failure precedes any mutation.
                opportunity.MoveToStage(
                    command.ToStage,
                    command.Probability ?? (sameStage ? opportunity.Probability : null),
                    sameStage ? opportunity.Probability : StageProbabilities[command.ToStage]);
            }

            await _crmRepository.UpdateOpportunityAsync(opportunity, cancellationToken);

            return Result<OpportunityDto>.Success(OpportunityDto.Build(opportunity));
        }
        catch (CRMValidationException ex)
        {
            return Result<OpportunityDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            // Spec CRM-06: the row moved between our load and our save (RowVersion race on
            // UpdateOpportunityAsync, translated by CrmRepository).
            return Result<OpportunityDto>.Failure(ex.Code, ex.Message);
        }
    }

    /// <summary>
    /// Normalized loss-reason resolution (CRM/ERPNext parity): an explicit catalog id wins
    /// (unknown/inactive ids reject with <c>crm_loss_reason_not_found</c>); legacy free text
    /// auto-matches a catalog code/name case-insensitively; anything else keeps the
    /// <c>crm_loss_reason_required</c> invariant (the close dialog always sends the id).
    /// </summary>
    private async Task<Guid> ResolveLossReasonIdAsync(
        Opportunity opportunity,
        AdvanceOpportunityStageCommand command,
        CancellationToken cancellationToken)
    {
        if (command.LossReasonId.HasValue && command.LossReasonId.Value != Guid.Empty)
        {
            if (_catalogs is null)
            {
                throw new CRMValidationException(
                    CRMErrorCodes.LossReasonRequired,
                    "A loss reason is mandatory when marking an opportunity as Closed Lost.");
            }

            var reason = await _catalogs.GetLossReasonByIdAsync(
                command.LossReasonId.Value, cancellationToken);
            if (reason is null || !reason.IsActive)
            {
                throw new CRMValidationException(
                    CRMErrorCodes.LossReasonNotFound,
                    $"Loss reason '{command.LossReasonId.Value}' was not found.");
            }

            return reason.Id;
        }

        if (!string.IsNullOrWhiteSpace(command.LossReason) && _catalogs is not null)
        {
            var text = command.LossReason.Trim();
            var reasons = await _catalogs.ListLossReasonsAsync(cancellationToken);
            var match = reasons.FirstOrDefault(r =>
                string.Equals(r.Code, text, StringComparison.OrdinalIgnoreCase)
                || string.Equals(r.Name, text, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match.Id;
            }
        }

        throw new CRMValidationException(
            CRMErrorCodes.LossReasonRequired,
            "A loss reason is mandatory when marking an opportunity as Closed Lost.");
    }

    private static void EnsureRowVersion(Opportunity opportunity, byte[]? expected)
    {
        if (expected is null)
        {
            return;
        }

        if (opportunity.RowVersion is null || !opportunity.RowVersion.AsSpan().SequenceEqual(expected))
        {
            throw new ConcurrencyConflictException(nameof(Opportunity), opportunity.Id);
        }
    }
}
