using Erp.Domain.Common;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// A qualified commercial deal actively pursued.
/// Ubiquitous language: "Opportunity" - .specify/modules/08-crm/spec.md §1.
/// </summary>
public class Opportunity : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string OpportunityNumber { get; set; } = string.Empty;

    /// <summary>'Lead' or 'Customer'</summary>
    public string OpportunityFrom { get; set; } = "Lead";

    /// <summary>Lead Id or Customer Id</summary>
    public Guid PartyId { get; set; }

    public string PartyName { get; set; } = string.Empty;

    public string Stage { get; set; } = OpportunityStage.Prospecting;

    public decimal OpportunityAmount { get; set; }
    public decimal Probability { get; set; } = 10.00m;

    /// <summary>Calculated automatically (Amount * Probability / 100)</summary>
    public decimal WeightedAmount => OpportunityAmount * (Probability / 100.0m);

    public string Currency { get; set; } = "USD";
    public DateOnly ExpectedClosingDate { get; set; }

    public string Status { get; set; } = OpportunityStatus.Open;
    public string? LossReason { get; set; }
    public Guid? AssignedSalespersonId { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public Company? Company { get; set; }

    /// <summary>
    /// Follow-up log attached to this deal (plan.md §1 CRMActivity; spec CRM-03 audit trail).
    /// Added Block A-fill: the adopted entity shipped without the collection side of the
    /// relationship, which the EF mapping and Block B conversion wiring require.
    /// </summary>
    public ICollection<CRMActivity> Activities { get; set; } = new List<CRMActivity>();

    /// <summary>
    /// Transitions the opportunity to ClosedWon.
    /// Invariant CRM-01: Probability = 100%.
    /// </summary>
    public void MarkAsClosedWon()
    {
        if (Status == OpportunityStatus.Won || Status == OpportunityStatus.Lost)
        {
            throw new CRMValidationException(
                CRMErrorCodes.OpportunityAlreadyClosed,
                $"Opportunity '{OpportunityNumber}' is already closed.");
        }

        Stage = OpportunityStage.ClosedWon;
        Status = OpportunityStatus.Won;
        Probability = 100.00m;
        LossReason = null;
    }

    /// <summary>
    /// Transitions the opportunity to ClosedLost.
    /// Invariant CRM-02: LossReason is required.
    /// Invariant CRM-01: Probability = 0%.
    /// </summary>
    public void MarkAsClosedLost(string lossReason)
    {
        if (string.IsNullOrWhiteSpace(lossReason))
        {
            throw new CRMValidationException(
                CRMErrorCodes.LossReasonRequired,
                "A Loss Reason is mandatory when marking an opportunity as Closed Lost.");
        }

        if (Status == OpportunityStatus.Won || Status == OpportunityStatus.Lost)
        {
            throw new CRMValidationException(
                CRMErrorCodes.OpportunityAlreadyClosed,
                $"Opportunity '{OpportunityNumber}' is already closed.");
        }

        Stage = OpportunityStage.ClosedLost;
        Status = OpportunityStatus.Lost;
        Probability = 0.00m;
        LossReason = lossReason;
    }

    /// <summary>
    /// Scenario CRM-05: Deal Cancellation & Re-opening Workflow
    /// Re-opens a lost deal back to Negotiation stage.
    /// </summary>
    public void Reopen(decimal newProbability = 50.00m)
    {
        Stage = OpportunityStage.Negotiation;
        Status = OpportunityStatus.Open;
        Probability = newProbability;
        LossReason = null;
    }
}
