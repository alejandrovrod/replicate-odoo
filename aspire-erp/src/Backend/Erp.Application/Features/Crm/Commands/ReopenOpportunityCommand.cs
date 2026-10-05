using Erp.Application.Common;
using System;

namespace Erp.Application.Features.Crm.Commands;

public record ReopenOpportunityCommand(
    Guid CompanyId,
    Guid OpportunityId,
    decimal NewProbability = 50.00m
) : ICommand<Result<Guid>>;
