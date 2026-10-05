using Erp.Domain.Entities;
using System.Collections.Generic;
using System.Linq;

namespace Erp.Domain.Services;

/// <summary>
/// Service for CRM pipeline forecasting logic.
/// </summary>
public sealed class PipelineForecaster
{
    /// <summary>
    /// Calculates the total weighted pipeline value for all given opportunities.
    /// Invariant CRM-01: WeightedForecast = Sum(OpportunityAmount * (Probability / 100))
    /// </summary>
    public static decimal CalculateWeightedPipeline(IEnumerable<Opportunity> opportunities)
    {
        return opportunities
            .Where(o => o.Status == OpportunityStatus.Open)
            .Sum(o => o.WeightedAmount);
    }
}
