using Erp.Domain.Entities;
using Erp.Domain.Exceptions;

namespace Erp.Application.Services;

/// <inheritdoc cref="ILandedCostCalculationService"/>
public sealed class LandedCostCalculationService : ILandedCostCalculationService
{
    public IReadOnlyList<LandedCostApportionmentResult> Apportion(
        IReadOnlyList<LandedCostApportionmentInput> lines,
        decimal totalCharges,
        LandedCostDistributeBy distributeBy)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
        {
            throw new LandedCostValidationException(
                LandedCostErrorCodes.PurchaseReceiptNotSubmitted,
                "Apportionment needs at least one receipt line.");
        }

        if (totalCharges <= 0m)
        {
            throw new LandedCostValidationException(
                LandedCostErrorCodes.ChargesRequired,
                $"Total charges must be greater than zero (received {totalCharges:0.####}).");
        }

        if (distributeBy is not LandedCostDistributeBy.Amount and not LandedCostDistributeBy.Quantity)
        {
            throw new LandedCostValidationException(
                LandedCostErrorCodes.InvalidDistributeBy,
                $"Unknown apportionment strategy '{(int)distributeBy}'.");
        }

        var weights = new decimal[lines.Count];
        var totalWeight = 0m;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Qty <= 0m)
            {
                throw new LandedCostValidationException(
                    LandedCostErrorCodes.InvalidDistributeBy,
                    $"Receipt line quantity must be greater than zero (received {line.Qty:0.####}).");
            }

            weights[i] = distributeBy == LandedCostDistributeBy.Amount ? line.Amount : line.Qty;
            if (weights[i] < 0m)
            {
                throw new LandedCostValidationException(
                    LandedCostErrorCodes.InvalidDistributeBy,
                    "Apportionment weights must not be negative.");
            }

            totalWeight += weights[i];
        }

        if (totalWeight <= 0m)
        {
            throw new LandedCostValidationException(
                LandedCostErrorCodes.InvalidDistributeBy,
                "Apportionment weights sum to zero: nothing to spread the charges over.");
        }

        // Exact-cents distribution: every line but the last rounds, the last absorbs the
        // residual - the shares always sum to totalCharges to the cent.
        var results = new List<LandedCostApportionmentResult>(lines.Count);
        var distributed = 0m;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            decimal share;
            if (i == lines.Count - 1)
            {
                share = totalCharges - distributed;
            }
            else
            {
                share = Math.Round(totalCharges * weights[i] / totalWeight, 2, MidpointRounding.AwayFromZero);
                distributed += share;
            }

            var newRate = Math.Round(
                (line.Amount + share) / line.Qty, 6, MidpointRounding.AwayFromZero);

            results.Add(new LandedCostApportionmentResult(
                line.PurchaseReceiptId,
                line.PurchaseReceiptLineId,
                line.ItemId,
                line.Qty,
                line.OriginalRate,
                share,
                newRate));
        }

        return results;
    }
}
