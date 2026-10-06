using Erp.Application.Features.Crm.Commands;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Erp.Application.UnitTests.Crm;

/// <summary>
/// Block B tests for the stage-advance command (tasks 11.2/11.5, specs CRM-02/CRM-06):
/// forward/backward moves with milestone probability sync, terminal guards, the mandatory
/// loss reason, RowVersion CAS pre-check plus repository conflict translation, and
/// zero-write proofs on every rejection.
/// </summary>
public class AdvanceOpportunityStageTests
{
    [Fact]
    public async Task HandleAsync_ShouldSyncMilestoneProbability_OnForwardMove()
    {
        var (handler, repo, opp) = Arrange(OpenOpportunity(OpportunityStage.Qualification, 25m));

        var result = await handler.HandleAsync(
            new AdvanceOpportunityStageCommand(opp.Id, opp.CompanyId, OpportunityStage.Negotiation));

        Assert.True(result.IsSuccess);
        Assert.Equal(OpportunityStage.Negotiation, opp.Stage);
        Assert.Equal(OpportunityStatus.Open, opp.Status);
        Assert.Equal(80m, opp.Probability);
        Assert.Equal(opp.OpportunityAmount * 0.8m, result.Value!.WeightedAmount);
        Assert.True(repo.UpdateCalled);
    }

    [Fact]
    public async Task HandleAsync_ShouldSyncMilestoneProbability_OnBackwardMove()
    {
        var (handler, _, opp) = Arrange(OpenOpportunity(OpportunityStage.Negotiation, 80m));

        var result = await handler.HandleAsync(
            new AdvanceOpportunityStageCommand(opp.Id, opp.CompanyId, OpportunityStage.Qualification));

        Assert.True(result.IsSuccess);
        Assert.Equal(OpportunityStage.Qualification, opp.Stage);
        Assert.Equal(25m, opp.Probability);
    }

    [Fact]
    public async Task HandleAsync_ShouldHonorExplicitProbability_WhenInRange()
    {
        var (handler, _, opp) = Arrange(OpenOpportunity(OpportunityStage.Qualification, 25m));

        var result = await handler.HandleAsync(
            new AdvanceOpportunityStageCommand(opp.Id, opp.CompanyId, OpportunityStage.Negotiation, Probability: 70m));

        Assert.True(result.IsSuccess);
        Assert.Equal(70m, opp.Probability);
    }

    [Fact]
    public async Task HandleAsync_ShouldKeepProbability_WhenSameStageWithoutOverride()
    {
        var (handler, _, opp) = Arrange(OpenOpportunity(OpportunityStage.Negotiation, 90m));

        var result = await handler.HandleAsync(
            new AdvanceOpportunityStageCommand(opp.Id, opp.CompanyId, OpportunityStage.Negotiation));

        Assert.True(result.IsSuccess);
        Assert.Equal(90m, opp.Probability);
    }

    [Fact]
    public async Task HandleAsync_ShouldForceFullProbability_OnClosedWon()
    {
        var (handler, _, opp) = Arrange(OpenOpportunity(OpportunityStage.Negotiation, 80m, lossReason: null));

        var result = await handler.HandleAsync(
            new AdvanceOpportunityStageCommand(opp.Id, opp.CompanyId, OpportunityStage.ClosedWon, Probability: 80m));

        Assert.True(result.IsSuccess);
        Assert.Equal(OpportunityStage.ClosedWon, opp.Stage);
        Assert.Equal(OpportunityStatus.Won, opp.Status);
        Assert.Equal(100m, opp.Probability);
        Assert.Equal(opp.OpportunityAmount, result.Value!.WeightedAmount);
    }

    [Fact]
    public async Task HandleAsync_ShouldForceZeroProbability_OnClosedLostWithReason()
    {
        var (handler, _, opp) = Arrange(OpenOpportunity(OpportunityStage.Negotiation, 80m));

        var result = await handler.HandleAsync(
            new AdvanceOpportunityStageCommand(
                opp.Id, opp.CompanyId, OpportunityStage.ClosedLost, LossReason: "Competitor priced 15% lower"));

        Assert.True(result.IsSuccess);
        Assert.Equal(OpportunityStage.ClosedLost, opp.Stage);
        Assert.Equal(OpportunityStatus.Lost, opp.Status);
        Assert.Equal(0m, opp.Probability);
        Assert.Equal(0m, result.Value!.WeightedAmount);
        Assert.Equal("Competitor priced 15% lower", opp.LossReason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_ShouldRejectClosedLost_WithoutLossReason(string? lossReason)
    {
        var (handler, repo, opp) = Arrange(OpenOpportunity(OpportunityStage.Negotiation, 80m));

        var result = await handler.HandleAsync(
            new AdvanceOpportunityStageCommand(opp.Id, opp.CompanyId, OpportunityStage.ClosedLost, LossReason: lossReason));

        Assert.False(result.IsSuccess);
        Assert.Equal(CRMErrorCodes.LossReasonRequired, result.Error!.Code);
        Assert.False(repo.UpdateCalled);
        Assert.Equal(OpportunityStage.Negotiation, opp.Stage);
    }

    [Theory]
    [InlineData(OpportunityStatus.Won, OpportunityStage.ClosedWon)]
    [InlineData(OpportunityStatus.Lost, OpportunityStage.ClosedLost)]
    public async Task HandleAsync_ShouldRejectTerminalOpportunity(string status, string stage)
    {
        var opp = OpenOpportunity(stage, 100m);
        opp.Status = status;
        var (handler, repo, _) = Arrange(opp);

        var result = await handler.HandleAsync(
            new AdvanceOpportunityStageCommand(opp.Id, opp.CompanyId, OpportunityStage.Negotiation));

        Assert.False(result.IsSuccess);
        Assert.Equal(CRMErrorCodes.InvalidStatusTransition, result.Error!.Code);
        Assert.False(repo.UpdateCalled);
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectUndefinedStage()
    {
        var (handler, repo, opp) = Arrange(OpenOpportunity(OpportunityStage.Qualification, 25m));

        var result = await handler.HandleAsync(
            new AdvanceOpportunityStageCommand(opp.Id, opp.CompanyId, "ContractSigned"));

        Assert.False(result.IsSuccess);
        Assert.Equal(CRMErrorCodes.InvalidOpportunityStage, result.Error!.Code);
        Assert.False(repo.UpdateCalled);
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectOutOfRangeProbability()
    {
        var (handler, repo, opp) = Arrange(OpenOpportunity(OpportunityStage.Qualification, 25m));

        var result = await handler.HandleAsync(
            new AdvanceOpportunityStageCommand(opp.Id, opp.CompanyId, OpportunityStage.Proposal, Probability: 140m));

        Assert.False(result.IsSuccess);
        Assert.Equal(CRMErrorCodes.InvalidProbabilityRange, result.Error!.Code);
        Assert.False(repo.UpdateCalled);
    }

    [Fact]
    public async Task HandleAsync_ShouldFailFast_OnStaleRowVersion()
    {
        var (handler, repo, opp) = Arrange(OpenOpportunity(OpportunityStage.Qualification, 25m));
        opp.RowVersion = new byte[] { 1, 2, 3, 4 };

        var result = await handler.HandleAsync(
            new AdvanceOpportunityStageCommand(
                opp.Id, opp.CompanyId, OpportunityStage.Negotiation, RowVersion: new byte[] { 9, 9, 9, 9 }));

        Assert.False(result.IsSuccess);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, result.Error!.Code);
        Assert.False(repo.UpdateCalled);
        Assert.Equal(OpportunityStage.Qualification, opp.Stage);
    }

    [Fact]
    public async Task HandleAsync_ShouldMapRepositoryConflict_ToConcurrencyConflict()
    {
        var (handler, repo, opp) = Arrange(OpenOpportunity(OpportunityStage.Qualification, 25m));
        repo.ThrowConflictOnUpdate = true;

        var result = await handler.HandleAsync(
            new AdvanceOpportunityStageCommand(opp.Id, opp.CompanyId, OpportunityStage.Negotiation));

        Assert.False(result.IsSuccess);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, result.Error!.Code);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnNotFound_WhenOpportunityMissing()
    {
        var repo = new FakeCrmRepository { OpportunityToReturn = null };
        var handler = new AdvanceOpportunityStageCommandHandler(repo);

        var result = await handler.HandleAsync(
            new AdvanceOpportunityStageCommand(Guid.NewGuid(), Guid.NewGuid(), OpportunityStage.Negotiation));

        Assert.False(result.IsSuccess);
        Assert.Equal("crm_opportunity_not_found", result.Error!.Code);
        Assert.False(repo.UpdateCalled);
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectCompanyMismatch()
    {
        var (handler, repo, opp) = Arrange(OpenOpportunity(OpportunityStage.Qualification, 25m));

        var result = await handler.HandleAsync(
            new AdvanceOpportunityStageCommand(opp.Id, Guid.NewGuid(), OpportunityStage.Negotiation));

        Assert.False(result.IsSuccess);
        Assert.Equal("crm_opportunity_company_mismatch", result.Error!.Code);
        Assert.False(repo.UpdateCalled);
    }

    private static Opportunity OpenOpportunity(string stage, decimal probability, string? lossReason = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            OpportunityNumber = "OPP-2026-00042",
            OpportunityFrom = "Lead",
            PartyId = Guid.NewGuid(),
            PartyName = "TechCorp",
            Stage = stage,
            Status = OpportunityStatus.Open,
            OpportunityAmount = 50000m,
            Probability = probability,
            Currency = "USD",
            LossReason = lossReason,
            RowVersion = new byte[] { 1, 2, 3, 4 }
        };

    private static (AdvanceOpportunityStageCommandHandler Handler, FakeCrmRepository Repo, Opportunity Opp) Arrange(Opportunity opp)
    {
        var repo = new FakeCrmRepository { OpportunityToReturn = opp };
        return (new AdvanceOpportunityStageCommandHandler(repo), repo, opp);
    }

    private sealed class FakeCrmRepository : ICrmRepository
    {
        public Opportunity? OpportunityToReturn { get; set; }
        public bool UpdateCalled { get; private set; }
        public bool ThrowConflictOnUpdate { get; set; }

        public Task<Lead?> GetLeadByIdAsync(Guid leadId, CancellationToken cancellationToken = default) => Task.FromResult<Lead?>(null);
        public Task<Opportunity?> GetOpportunityByIdAsync(Guid opportunityId, CancellationToken cancellationToken = default) => Task.FromResult(OpportunityToReturn);
        public Task AddLeadAsync(Lead lead, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateLeadAsync(Lead lead, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddOpportunityAsync(Opportunity opportunity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateOpportunityAsync(Opportunity opportunity, CancellationToken cancellationToken = default)
        {
            UpdateCalled = true;
            if (ThrowConflictOnUpdate)
            {
                throw new ConcurrencyConflictException(nameof(Opportunity), opportunity.Id);
            }
            return Task.CompletedTask;
        }
        public Task<string> NextOpportunityNumberAsync(Guid companyId, int year, CancellationToken cancellationToken = default) => Task.FromResult("OPP-1");
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) => operation(cancellationToken);
        public Task<Lead?> GetLeadByDedupKeyAsync(Guid companyId, string source, string externalReference, CancellationToken cancellationToken = default) => Task.FromResult<Lead?>(null);
        public Task<PagedResult<Lead>> ListLeadsAsync(Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<Lead>(new List<Lead>(), 0, paging.SafePageNumber, paging.SafePageSize));
        public Task<PagedResult<Opportunity>> ListOpportunitiesAsync(Guid companyId, PagedRequest paging, string? stage, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<Opportunity>(new List<Opportunity>(), 0, paging.SafePageNumber, paging.SafePageSize));
    }
}
