using Erp.Domain.Entities;
using Erp.Domain.Exceptions;

using System;
using Xunit;

namespace Erp.Domain.UnitTests;

public class OpportunityTests
{
    [Fact]
    public void WeightedAmount_ShouldBeCalculatedBasedOnAmountAndProbability()
    {
        var opp = new Opportunity { OpportunityAmount = 1000m, Probability = 50m };
        Assert.Equal(500m, opp.WeightedAmount);
    }

    [Fact]
    public void MarkAsClosedLost_WithReason_ShouldSucceed()
    {
        var opp = new Opportunity { Stage = OpportunityStage.Negotiation, Status = OpportunityStatus.Open };
        opp.MarkAsClosedLost("Too expensive");

        Assert.Equal(OpportunityStage.ClosedLost, opp.Stage);
        Assert.Equal(OpportunityStatus.Lost, opp.Status);
        Assert.Equal(0m, opp.Probability);
        Assert.Equal("Too expensive", opp.LossReason);
    }

    [Fact]
    public void MarkAsClosedLost_WithoutReason_ShouldThrow()
    {
        var opp = new Opportunity { Stage = OpportunityStage.Negotiation, Status = OpportunityStatus.Open };

        var act = () => opp.MarkAsClosedLost("   ");

        var ex = Assert.Throws<CRMValidationException>(act);
        Assert.Contains("loss reason", ex.Message.ToLower());
    }

    [Fact]
    public void Reopen_FromClosedLost_ShouldSetStageToNegotiation()
    {
        var opp = new Opportunity { Stage = OpportunityStage.ClosedLost, Status = OpportunityStatus.Lost, LossReason = "Lost" };
        opp.Reopen(50m);

        Assert.Equal(OpportunityStage.Negotiation, opp.Stage);
        Assert.Equal(OpportunityStatus.Open, opp.Status);
        Assert.Equal(50m, opp.Probability);
        Assert.Null(opp.LossReason);
    }
}
