using System;

namespace AssetHub.Domain.Assets;

public class AssetHealthPrediction
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    public decimal RiskProbability { get; set; } // 0.0000 to 1.0000
    public string RiskLevel { get; set; } = "Low"; // Low, Moderate, High, Critical
    public int? PredictedFailureDays { get; set; } // Estimated days to failure (RUL)

    public string? TopFeatureContributionsJson { get; set; } // SHAP / Feature importance explanation

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
