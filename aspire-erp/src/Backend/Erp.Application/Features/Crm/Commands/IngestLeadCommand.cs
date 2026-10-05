using Erp.Application.Common;
using Erp.Application.Features.Crm.DTOs;
using System;

namespace Erp.Application.Features.Crm.Commands;

/// <summary>
/// Idempotent webhook intake (Block B, spec CRM-04): when <c>DeduplicationKey</c> is present
/// and a lead with the same (CompanyId, Source, DeduplicationKey) exists, the existing lead
/// is returned with <c>Duplicate=true</c> and nothing is inserted.
/// </summary>
public record IngestLeadCommand(
    Guid CompanyId,
    string LeadCode,
    string LeadName,
    string? OrganizationName = null,
    string? Email = null,
    string? Phone = null,
    string Source = "Website",
    string? DeduplicationKey = null
) : ICommand<Result<IngestLeadResultDto>>;
