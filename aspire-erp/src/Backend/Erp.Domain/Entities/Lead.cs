using Erp.Domain.Common;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Unqualified individual or organization expressing interest in company products or services.
/// Ubiquitous language: "Lead" - .specify/modules/08-crm/spec.md §1.
/// </summary>
public class Lead : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Company this lead belongs to.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Lead code, unique per tenant+company.</summary>
    public string LeadCode { get; set; } = string.Empty;

    /// <summary>Contact person's full name.</summary>
    public string LeadName { get; set; } = string.Empty;

    /// <summary>Name of the prospect's organization, if any.</summary>
    public string? OrganizationName { get; set; }

    /// <summary>Contact email.</summary>
    public string? Email { get; set; }

    /// <summary>Contact phone number.</summary>
    public string? Phone { get; set; }

    /// <summary>Origin of the lead (e.g., 'Website', 'Cold Call', 'Trade Show').</summary>
    public string Source { get; set; } = "Website";

    /// <summary>Current stage in the lead lifecycle (Open, Contacted, Qualified, Converted, Lost).</summary>
    public string Status { get; set; } = LeadStatus.Open;

    /// <summary>User ID of the assigned salesperson.</summary>
    public Guid? AssignedToUserId { get; set; }

    /// <summary>If converted, the resulting Opportunity ID.</summary>
    public Guid? ConvertedOpportunityId { get; set; }

    /// <summary>If converted, the resulting Customer ID.</summary>
    public Guid? ConvertedCustomerId { get; set; }

    /// <summary>Indicates if the lead is actively being pursued.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// External deduplication reference for webhook intake (Block B, spec CRM-04): the caller's
    /// idempotency/webhook id. Null for manually created leads. The (CompanyId, Source,
    /// ExternalReference) triple identifies a replay - the column is added here with its EF
    /// mapping, the physical migration lands in Block C.
    /// </summary>
    public string? ExternalReference { get; set; }

    public Company? Company { get; set; }

    /// <summary>
    /// Transitions the lead to Converted.
    /// Invariant CRM-03: The original Lead status becomes Converted.
    /// </summary>
    public void MarkAsConverted(Guid? opportunityId, Guid? customerId)
    {
        if (Status == LeadStatus.Converted)
        {
            throw new CRMValidationException(
                CRMErrorCodes.LeadAlreadyConverted,
                $"Lead '{LeadCode}' is already converted.");
        }

        Status = LeadStatus.Converted;
        ConvertedOpportunityId = opportunityId;
        ConvertedCustomerId = customerId;
    }
}
