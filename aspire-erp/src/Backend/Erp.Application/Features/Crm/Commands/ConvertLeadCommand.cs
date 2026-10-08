using Erp.Application.Common;
using Erp.Application.Features.Crm.DTOs;
using System;

namespace Erp.Application.Features.Crm.Commands;

/// <summary>
/// Converts a Lead into a Customer and an Opportunity. The opportunity opens at the spec
/// CRM-01 Qualification milestone (stage <c>Qualification</c>, 25% probability) unless the
/// caller states explicit terms.
/// </summary>
public record ConvertLeadCommand(
    Guid CompanyId,
    Guid LeadId,
    string CustomerCode,
    Guid? DefaultCurrencyId = null,
    int PaymentTermsDays = 30,
    decimal OpportunityAmount = 0m,
    decimal OpportunityProbability = 25m,
    DateOnly? ExpectedClosingDate = null,
    Guid? ConvertedByUserId = null
) : ICommand<Result<ConvertLeadResultDto>>;
