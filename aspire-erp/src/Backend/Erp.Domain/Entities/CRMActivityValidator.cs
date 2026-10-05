using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Domain rules for <see cref="CRMActivity"/> (mirrors LeadValidator / OpportunityValidator).
/// </summary>
public static class CRMActivityValidator
{
    public const int MaxSubjectLength = 200;

    public static void EnsureValidActivityFields(
        Guid opportunityId,
        string? subject,
        Guid createdByUserId)
    {
        if (opportunityId == Guid.Empty)
        {
            throw new CRMValidationException(
                "crm_activity_opportunity_required",
                "An activity must belong to an opportunity.");
        }

        if (string.IsNullOrWhiteSpace(subject) || subject.Length > MaxSubjectLength)
        {
            throw new CRMValidationException(
                "crm_activity_subject_invalid",
                $"Subject is required and cannot exceed {MaxSubjectLength} characters.");
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new CRMValidationException(
                "crm_activity_author_required",
                "An activity must record its author (CreatedByUserId is required).");
        }
    }
}
