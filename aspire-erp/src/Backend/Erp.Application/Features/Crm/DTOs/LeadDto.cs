using Erp.Domain.Entities;

namespace Erp.Application.Features.Crm.DTOs;

/// <summary>Lead read model (Block B ingest/list/convert reads).</summary>
public sealed record LeadDto(
    Guid Id,
    Guid CompanyId,
    string LeadCode,
    string LeadName,
    string? OrganizationName,
    string? Email,
    string? Phone,
    string Source,
    string Status)
{
    public static LeadDto Build(Lead lead) =>
        new(
            lead.Id,
            lead.CompanyId,
            lead.LeadCode,
            lead.LeadName,
            lead.OrganizationName,
            lead.Email,
            lead.Phone,
            lead.Source,
            lead.Status);
}
