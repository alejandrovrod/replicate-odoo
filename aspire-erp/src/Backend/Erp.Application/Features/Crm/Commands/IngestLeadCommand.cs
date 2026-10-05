using Erp.Application.Common;
using System;

namespace Erp.Application.Features.Crm.Commands;

public record IngestLeadCommand(
    Guid CompanyId,
    string LeadCode,
    string LeadName,
    string? OrganizationName = null,
    string? Email = null,
    string? Phone = null,
    string Source = "Website"
) : ICommand<Result<Guid>>;
