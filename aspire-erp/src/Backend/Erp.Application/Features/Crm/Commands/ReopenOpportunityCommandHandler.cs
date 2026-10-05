using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Erp.Application.Features.Crm.Commands;

public sealed class ReopenOpportunityCommandHandler : ICommandHandler<ReopenOpportunityCommand, Result<Guid>>
{
    private readonly ICrmRepository _crmRepository;

    public ReopenOpportunityCommandHandler(ICrmRepository crmRepository)
    {
        _crmRepository = crmRepository;
    }

    public async Task<Result<Guid>> HandleAsync(ReopenOpportunityCommand command, CancellationToken cancellationToken = default)
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

            if (opportunity.Status != OpportunityStatus.Lost)
            {
                throw new CRMValidationException(
                    "crm_opportunity_not_lost",
                    "Only closed lost opportunities can be re-opened.");
            }

            if (command.NewProbability < 0 || command.NewProbability > 100)
            {
                throw new CRMValidationException(
                    CRMErrorCodes.InvalidProbabilityRange,
                    $"Probability must be between 0 and 100. Received {command.NewProbability}.");
            }

            opportunity.Reopen(command.NewProbability);
            
            await _crmRepository.UpdateOpportunityAsync(opportunity, cancellationToken);

            return Result<Guid>.Success(opportunity.Id);
        }
        catch (CRMValidationException ex)
        {
            return Result<Guid>.Failure(ex.Code, ex.Message);
        }
    }
}
