using Erp.Application.Common;
using Erp.Application.Features.Crm.DTOs;
using System;

namespace Erp.Application.Features.Crm.Commands;

/// <summary>
/// Converts a Lead into a Customer and an Opportunity.
/// </summary>
public record ConvertLeadCommand(
    Guid CompanyId,
    Guid LeadId,
    string CustomerCode,
    string? DefaultCurrency = "USD",
    int PaymentTermsDays = 30,
    decimal OpportunityAmount = 0m,
    decimal OpportunityProbability = 10m,
    DateOnly? ExpectedClosingDate = null,
    Guid? ConvertedByUserId = null
) : ICommand<Result<ConvertLeadResultDto>>;
