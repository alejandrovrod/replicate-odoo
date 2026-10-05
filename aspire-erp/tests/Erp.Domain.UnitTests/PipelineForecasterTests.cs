using Erp.Domain.Entities;
using Erp.Domain.Services;
using System;
using System.Collections.Generic;
using Xunit;

namespace Erp.Domain.UnitTests;

public class PipelineForecasterTests
{
    [Fact]
    public void CalculateTotalWeightedPipeline_ShouldSumWeightedAmounts()
    {
        // Arrange
        var opp1 = new Opportunity { OpportunityAmount = 1000m, Probability = 50m };
        var opp2 = new Opportunity { OpportunityAmount = 2000m, Probability = 25m };
        var opp3 = new Opportunity { OpportunityAmount = 500m, Probability = 100m };

        // Act
        var result = PipelineForecaster.CalculateWeightedPipeline(new[] { opp1, opp2, opp3 });

        // Assert
        Assert.Equal(1500m, result);
    }
}
