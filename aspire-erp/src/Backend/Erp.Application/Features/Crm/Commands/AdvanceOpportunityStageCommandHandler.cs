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

    public AdvanceOpportunityStageCommandHandler(ICrmRepository crmRepository)
    {
        _crmRepository = crmRepository;
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
                // MarkAsClosedLost is the single rule source: empty reason throws
                // crm_loss_reason_required (spec CRM-02, OpportunityValidator shape mirrored
                // inside the entity) and forces Probability=0.
                opportunity.MarkAsClosedLost(command.LossReason!);
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
                opportunity.Stage = command.ToStage;
                opportunity.Status = OpportunityStatus.Open;
                opportunity.LossReason = null;
                opportunity.Probability = command.Probability
                    ?? (sameStage ? opportunity.Probability : StageProbabilities[command.ToStage]);
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
