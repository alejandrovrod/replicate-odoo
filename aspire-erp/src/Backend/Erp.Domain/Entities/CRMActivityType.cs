namespace Erp.Domain.Entities;

/// <summary>
/// Follow-up log entry kinds for <see cref="CRMActivity"/> (plan.md §1 CRMActivity.Type:
/// Call, Email, Meeting, Task, Note). Persisted as the enum NAME (AccountConfiguration
/// RootType precedent) so history rows stay human-readable.
/// </summary>
public enum CRMActivityType
{
    Call,
    Email,
    Meeting,
    Task,
    Note
}
