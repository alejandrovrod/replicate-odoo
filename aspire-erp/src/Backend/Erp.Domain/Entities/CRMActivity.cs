namespace Erp.Domain.Entities;

/// <summary>
/// Follow-up log entry attached to an <see cref="Opportunity"/> (plan.md §1 CRMActivity).
/// The missing plan table that spec invariant CRM-03's audit-trail acceptance requires:
/// communication logs and activity notes stay attached to the deal across the lead-conversion
/// boundary (Block B wires the copy/reference into ConvertLead; this block ships the entity).
/// </summary>
/// <remarks>
/// Tenancy is inherited from the parent opportunity (SalesOrderItem/JournalEntryLine
/// precedent): no <c>TenantId</c> of its own, so no direct global query filter - every read
/// goes through the tenant-scoped opportunity.
/// </remarks>
public sealed class CRMActivity
{
    public Guid Id { get; set; }

    public Guid OpportunityId { get; set; }

    public Opportunity? Opportunity { get; set; }

    public CRMActivityType Type { get; set; } = CRMActivityType.Note;

    /// <summary>Short summary (required, max 200 chars, plan.md §1).</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Free-form body (optional, plan.md §1).</summary>
    public string? Content { get; set; }

    /// <summary>When the activity happened; defaults to now (plan.md §1 SYSDATETIMEOFFSET()).</summary>
    public DateTimeOffset ActivityDate { get; set; } = DateTimeOffset.UtcNow;

    public DateOnly? NextFollowUpDate { get; set; }

    public Guid CreatedByUserId { get; set; }
}
