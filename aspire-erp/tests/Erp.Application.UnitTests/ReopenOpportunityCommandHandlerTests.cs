using Erp.Application.Features.Crm.Commands;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;


using System;
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
    }
}
