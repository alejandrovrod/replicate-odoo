using Erp.Domain.Entities;

namespace Erp.Application.Services;

/// <summary>One receipt line entering apportionment (qty + value weight + original rate).</summary>
public sealed record LandedCostApportionmentInput(
    Guid PurchaseReceiptId,
    Guid PurchaseReceiptLineId,
    Guid ItemId,
    decimal Qty,
    decimal Amount,
    decimal OriginalRate);

/// <summary>One apportioned line: charges share (exact cents) + new valuation rate.</summary>
public sealed record LandedCostApportionmentResult(
    Guid PurchaseReceiptId,
    Guid PurchaseReceiptLineId,
    Guid ItemId,
    decimal Qty,
    decimal OriginalRate,
    decimal ApplicableCharges,
    decimal NewRate);

/// <summary>
/// Deterministic landed-cost apportionment (Fase 4, T2): spreads a charge total over
/// receipt lines by value (<c>Amount</c>) or quantity (<c>Quantity</c>), purely in memory.
/// Per-line shares round to 2 decimals with the residual absorbed by the last line, so the
/// shares sum to EXACTLY the charge total; new rates round to 6 (rate-column precision).
/// </summary>
public interface ILandedCostCalculationService
{
    IReadOnlyList<LandedCostApportionmentResult> Apportion(
        IReadOnlyList<LandedCostApportionmentInput> lines,
        decimal totalCharges,
        LandedCostDistributeBy distributeBy);
}
