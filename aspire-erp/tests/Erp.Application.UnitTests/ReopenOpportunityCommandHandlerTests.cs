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

public class ReopenOpportunityCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldReopenOpportunity_WhenOpportunityIsLost()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var oppId = Guid.NewGuid();
        
        var opp = new Opportunity 
        { 
            Id = oppId, 
            CompanyId = companyId,
            Status = OpportunityStatus.Lost,
            Stage = OpportunityStage.ClosedLost,
            LossReason = "Old reason"
        };
        
        var repo = new FakeCrmRepository { OpportunityToReturn = opp };
                
        var handler = new ReopenOpportunityCommandHandler(repo);
        var cmd = new ReopenOpportunityCommand(companyId, oppId, 45m);

        // Act
        var result = await handler.HandleAsync(cmd);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OpportunityStage.Negotiation, opp.Stage);
        Assert.Equal(OpportunityStatus.Open, opp.Status);
        Assert.Equal(45m, opp.Probability);
        
        Assert.True(repo.UpdateOpportunityCalled);
    }

    [Fact]
    public async Task HandleAsync_ShouldFail_WhenOpportunityNotLost()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var oppId = Guid.NewGuid();
        
        var opp = new Opportunity 
        { 
            Id = oppId, 
            CompanyId = companyId,
            Status = OpportunityStatus.Open,
            Stage = OpportunityStage.Negotiation
        };
        
        var repo = new FakeCrmRepository { OpportunityToReturn = opp };
                
        var handler = new ReopenOpportunityCommandHandler(repo);
        var cmd = new ReopenOpportunityCommand(companyId, oppId, 50m);

        // Act
        var result = await handler.HandleAsync(cmd);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("crm_opportunity_not_lost", result.Error.Code);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(-0.5)]
    [InlineData(100.5)]
    [InlineData(150)]
    public async Task HandleAsync_ShouldRejectOutOfRangeProbability_WithZeroWrites(double probabilityRaw)
    {
        // Arrange (fix-pass S3): the same 0-100 guard the advance handler carries - a
        // reopen must not smuggle an impossible probability onto the deal.
        var companyId = Guid.NewGuid();
        var oppId = Guid.NewGuid();

        var opp = new Opportunity
        {
            Id = oppId,
            CompanyId = companyId,
            Status = OpportunityStatus.Lost,
            Stage = OpportunityStage.ClosedLost,
            LossReason = "Budget frozen",
            Probability = 0m,
        };

        var repo = new FakeCrmRepository { OpportunityToReturn = opp };

        var handler = new ReopenOpportunityCommandHandler(repo);
        var cmd = new ReopenOpportunityCommand(companyId, oppId, (decimal)probabilityRaw);

        // Act
        var result = await handler.HandleAsync(cmd);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(CRMErrorCodes.InvalidProbabilityRange, result.Error!.Code);
        Assert.False(repo.UpdateOpportunityCalled);
        Assert.Equal(OpportunityStage.ClosedLost, opp.Stage);
        Assert.Equal(OpportunityStatus.Lost, opp.Status);
        Assert.Equal("Budget frozen", opp.LossReason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task HandleAsync_ShouldAcceptBoundaryProbabilities(decimal probability)
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var oppId = Guid.NewGuid();

        var opp = new Opportunity
        {
            Id = oppId,
            CompanyId = companyId,
            Status = OpportunityStatus.Lost,
            Stage = OpportunityStage.ClosedLost,
            LossReason = "Budget frozen",
            Probability = 0m,
        };

        var repo = new FakeCrmRepository { OpportunityToReturn = opp };

        var handler = new ReopenOpportunityCommandHandler(repo);
        var cmd = new ReopenOpportunityCommand(companyId, oppId, probability);

        // Act
        var result = await handler.HandleAsync(cmd);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(probability, opp.Probability);
        Assert.True(repo.UpdateOpportunityCalled);
    }

    private class FakeCrmRepository : ICrmRepository
    {
        public Opportunity OpportunityToReturn { get; set; }
        public bool UpdateOpportunityCalled { get; set; }

        public Task<Lead?> GetLeadByIdAsync(Guid leadId, CancellationToken cancellationToken = default) => Task.FromResult<Lead?>(null);
        public Task<Opportunity?> GetOpportunityByIdAsync(Guid opportunityId, CancellationToken cancellationToken = default) => Task.FromResult<Opportunity?>(OpportunityToReturn);
        public Task AddLeadAsync(Lead lead, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateLeadAsync(Lead lead, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddOpportunityAsync(Opportunity opportunity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateOpportunityAsync(Opportunity opportunity, CancellationToken cancellationToken = default) { UpdateOpportunityCalled = true; return Task.CompletedTask; }
        public Task<string> NextOpportunityNumberAsync(Guid companyId, int year, CancellationToken cancellationToken = default) => Task.FromResult("OPP-1");
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) => operation(cancellationToken);
        public Task<Lead?> GetLeadByDedupKeyAsync(Guid companyId, string source, string externalReference, CancellationToken cancellationToken = default) => Task.FromResult<Lead?>(null);
        public Task<PagedResult<Lead>> ListLeadsAsync(Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<Lead>(new List<Lead>(), 0, paging.SafePageNumber, paging.SafePageSize));
        public Task<PagedResult<Opportunity>> ListOpportunitiesAsync(Guid companyId, PagedRequest paging, string? stage, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<Opportunity>(new List<Opportunity>(), 0, paging.SafePageNumber, paging.SafePageSize));
    }
}
