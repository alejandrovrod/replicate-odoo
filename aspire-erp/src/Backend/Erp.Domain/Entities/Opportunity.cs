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

    /// <summary>
    /// Deal currency (RM-09 FK to the global <see cref="Currency"/> catalog);
    /// null means "USD" for legacy rows.
    /// </summary>
    public Guid? CurrencyId { get; set; }

    public Currency? Currency { get; set; }

    public DateOnly ExpectedClosingDate { get; set; }
    public string Status { get; set; } = OpportunityStatus.Open;

    /// <summary>
    /// Normalized loss reason (FK to the <see cref="OpportunityLossReason"/> catalog);
    /// required when closing as Lost. The free-text <see cref="LossReason"/> below carries
    /// the additional notes.
    /// </summary>
    public Guid? LossReasonId { get; set; }

    public OpportunityLossReason? LossReasonEntry { get; set; }

    /// <summary>Additional loss notes (the former free-text reason column, kept as notes).</summary>
    public string? LossReason { get; set; }

    /// <summary>Segmentation: sales territory of the deal (nullable for legacy rows).</summary>
    public Guid? TerritoryId { get; set; }

    public Territory? Territory { get; set; }

    /// <summary>Segmentation: industry of the deal (nullable for legacy rows).</summary>
    public Guid? IndustryId { get; set; }

    public Industry? Industry { get; set; }

    /// <summary>Segmentation: free-text market segment (e.g. SMB, Enterprise).</summary>
    public string? MarketSegment { get; set; }

    /// <summary>
    /// Named competitors on the deal (ERPNext Competitors child-table parity, stored as a
    /// JSON value object - a fixed small string set, never queried by element).
    /// </summary>
    public List<string> Competitors { get; set; } = new();

    /// <summary>
    /// Deal-currency to company-currency rate at pricing time (1 when the deal currency IS
    /// the company currency or no currency is set). Drives <see cref="BaseOpportunityAmount"/>.
    /// </summary>
    public decimal ExchangeRate { get; set; } = 1m;

    /// <summary>
    /// ERPNext <c>base_opportunity_amount</c>: header total converted to company currency
    /// (kept by <see cref="RecalculateTotals"/>).
    /// </summary>
    public decimal BaseOpportunityAmount { get; set; }

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
    /// Priced product/service breakdown of the deal (CRM/ERPNext parity). Empty for
    /// amount-only deals (e.g. converted leads): the header amount then stands alone.
    /// </summary>
    public ICollection<OpportunityItem> Items { get; set; } = new List<OpportunityItem>();

    /// <summary>Quotations issued from this deal (the ERPNext pre-order flow).</summary>
    public ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();

    /// <summary>
    /// Recomputes the header money from the priced lines (ERPNext item-driven totals):
    /// header amount = Σ line nets, base amount = header × exchange rate. A deal WITHOUT
    /// lines keeps its scalar header amount (converted-lead parity) and only refreshes
    /// the base conversion. Weighted stays a computed CLR property.
    /// </summary>
    public void RecalculateTotals()
    {
        if (Items.Count > 0)
        {
            OpportunityAmount = Math.Round(
                Items.Sum(i => i.Quantity * i.Rate * (1m - i.DiscountPercentage / 100m)),
                2,
                MidpointRounding.AwayFromZero);
        }

        BaseOpportunityAmount = Math.Round(
            OpportunityAmount * ExchangeRate, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Non-terminal pipeline move (the single rule source for probability sync on plain
    /// advances): sets the stage, keeps Open status, clears any loss data and syncs the
    /// probability to the milestone default unless the caller pins one.
    /// </summary>
    /// <exception cref="CRMValidationException">
    /// <c>invalid_status_transition</c> when the deal is already terminal.
    /// </exception>
    public void MoveToStage(string stage, decimal? probability, decimal milestoneProbability)
    {
        if (Status is OpportunityStatus.Won or OpportunityStatus.Lost or OpportunityStatus.Expired)
        {
            throw new CRMValidationException(
                CRMErrorCodes.InvalidStatusTransition,
                $"Opportunity '{OpportunityNumber}' is '{Status}' and cannot advance; re-open it first.");
        }

        Stage = stage;
        Status = OpportunityStatus.Open;
        LossReasonId = null;
        LossReason = null;
        Probability = probability ?? milestoneProbability;
    }

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
        LossReasonId = null;
        LossReason = null;
    }

    /// <summary>
    /// Transitions the opportunity to ClosedLost.
    /// Invariant CRM-02: a catalog loss reason is required (free-text notes optional).
    /// Invariant CRM-01: Probability = 0%.
    /// </summary>
    public void MarkAsClosedLost(Guid? lossReasonId, string? lossNotes = null)
    {
        if (!lossReasonId.HasValue || lossReasonId.Value == Guid.Empty)
        {
            throw new CRMValidationException(
                CRMErrorCodes.LossReasonRequired,
                "A loss reason is mandatory when marking an opportunity as Closed Lost.");
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
        LossReasonId = lossReasonId;
        LossReason = string.IsNullOrWhiteSpace(lossNotes) ? null : lossNotes.Trim();
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
        LossReasonId = null;
        LossReason = null;
    }
}
