using Erp.Domain.Entities;
using Erp.Domain.Exceptions;

using System;
using System.Collections.Generic;
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
        var reasonId = Guid.NewGuid();
        var opp = new Opportunity { Stage = OpportunityStage.Negotiation, Status = OpportunityStatus.Open };
        opp.MarkAsClosedLost(reasonId, "Priced 15% lower");

        Assert.Equal(OpportunityStage.ClosedLost, opp.Stage);
        Assert.Equal(OpportunityStatus.Lost, opp.Status);
        Assert.Equal(0m, opp.Probability);
        Assert.Equal(reasonId, opp.LossReasonId);
        Assert.Equal("Priced 15% lower", opp.LossReason);
    }

    [Fact]
    public void MarkAsClosedLost_WithoutReason_ShouldThrow()
    {
        var opp = new Opportunity { Stage = OpportunityStage.Negotiation, Status = OpportunityStatus.Open };

        var act = () => opp.MarkAsClosedLost(null, "   ");

        var ex = Assert.Throws<CRMValidationException>(act);
        Assert.Equal(CRMErrorCodes.LossReasonRequired, ex.Code);
    }

    [Fact]
    public void MarkAsClosedLost_WithoutNotes_ClearsNotes()
    {
        var opp = new Opportunity { Stage = OpportunityStage.Negotiation, Status = OpportunityStatus.Open };
        opp.MarkAsClosedLost(Guid.NewGuid());

        Assert.NotNull(opp.LossReasonId);
        Assert.Null(opp.LossReason);
    }

    [Fact]
    public void Reopen_FromClosedLost_ShouldSetStageToNegotiation()
    {
        var opp = new Opportunity { Stage = OpportunityStage.ClosedLost, Status = OpportunityStatus.Lost, LossReasonId = Guid.NewGuid(), LossReason = "Lost" };
        opp.Reopen(50m);

        Assert.Equal(OpportunityStage.Negotiation, opp.Stage);
        Assert.Equal(OpportunityStatus.Open, opp.Status);
        Assert.Equal(50m, opp.Probability);
        Assert.Null(opp.LossReasonId);
        Assert.Null(opp.LossReason);
    }

    [Fact]
    public void RecalculateTotals_WithLines_SumsNetsAndConvertsBase()
    {
        var opp = new Opportunity
        {
            OpportunityAmount = 999m,
            ExchangeRate = 1.25m,
            Items = new List<OpportunityItem>
            {
                new() { Quantity = 2m, Rate = 100m, DiscountPercentage = 10m },
                new() { Quantity = 1m, Rate = 50m, DiscountPercentage = 0m },
            },
        };

        opp.RecalculateTotals();

        // (2×100×0.9) + (1×50) = 230; base = 230 × 1.25 = 287.50.
        Assert.Equal(230m, opp.OpportunityAmount);
        Assert.Equal(287.50m, opp.BaseOpportunityAmount);
    }

    [Fact]
    public void RecalculateTotals_WithoutLines_KeepsScalarAndRefreshesBase()
    {
        var opp = new Opportunity { OpportunityAmount = 1000m, ExchangeRate = 2m };

        opp.RecalculateTotals();

        Assert.Equal(1000m, opp.OpportunityAmount);
        Assert.Equal(2000m, opp.BaseOpportunityAmount);
    }

    [Fact]
    public void MoveToStage_PlainMove_SyncsMilestoneAndClearsLoss()
    {
        var opp = new Opportunity
        {
            Stage = OpportunityStage.Qualification,
            Status = OpportunityStatus.Open,
            Probability = 25m,
            LossReasonId = Guid.NewGuid(),
            LossReason = "stale",
        };

        opp.MoveToStage(OpportunityStage.Negotiation, probability: null, milestoneProbability: 80m);

        Assert.Equal(OpportunityStage.Negotiation, opp.Stage);
        Assert.Equal(OpportunityStatus.Open, opp.Status);
        Assert.Equal(80m, opp.Probability);
        Assert.Null(opp.LossReasonId);
        Assert.Null(opp.LossReason);
    }

    [Fact]
    public void MoveToStage_SameStage_KeepsPinnedProbability()
    {
        var opp = new Opportunity
        {
            Stage = OpportunityStage.Negotiation,
            Status = OpportunityStatus.Open,
            Probability = 90m,
        };

        opp.MoveToStage(OpportunityStage.Negotiation, probability: 90m, milestoneProbability: 80m);

        Assert.Equal(90m, opp.Probability);
    }

    [Fact]
    public void MoveToStage_FromTerminal_ThrowsInvalidTransition()
    {
        var opp = new Opportunity { Stage = OpportunityStage.ClosedWon, Status = OpportunityStatus.Won };

        var ex = Assert.Throws<CRMValidationException>(
            () => opp.MoveToStage(OpportunityStage.Negotiation, null, 80m));
        Assert.Equal(CRMErrorCodes.InvalidStatusTransition, ex.Code);
    }
}
