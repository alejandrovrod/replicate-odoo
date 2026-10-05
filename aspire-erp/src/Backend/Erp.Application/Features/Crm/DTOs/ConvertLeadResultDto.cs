using System;

namespace Erp.Application.Features.Crm.DTOs;

public record ConvertLeadResultDto(
    Guid LeadId,
    Guid CustomerId,
    string CustomerCode,
    Guid OpportunityId,
    string OpportunityNumber,
    decimal WeightedPipelineAmount
);
