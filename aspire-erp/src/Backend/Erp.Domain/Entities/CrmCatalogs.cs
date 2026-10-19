using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Normalized loss-reason catalog (CRM/ERPNext parity): deals close as Lost against one of
/// these rows instead of free text, so loss analytics can aggregate. The free-text column
/// <see cref="Opportunity.LossReason"/> survives as the additional-notes field.
/// </summary>
public sealed class OpportunityLossReason : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Sales territory master (CRM segmentation parity): deals and customers segment by one
/// of these rows. Read-only catalog seeded per tenant; no CRUD surface by design.
/// </summary>
public sealed class Territory : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Industry master (CRM segmentation parity): deals segment by one of these rows.
/// Read-only catalog seeded per tenant; no CRUD surface by design.
/// </summary>
public sealed class Industry : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
