using Erp.Application.Features.Crm.Commands;
using Erp.Application.Features.Crm.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;


using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Erp.Application.UnitTests.Crm;

public class ConvertLeadCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldConvertLead_WhenValid()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        
        var companyRepo = new FakeCompanyRepository { CompanyToReturn = new Company { Id = companyId } };
        var lead = new Lead { Id = leadId, CompanyId = companyId, Status = LeadStatus.Open, LeadCode = "L-01", LeadName = "Test Lead" };
        var crmRepo = new FakeCrmRepository { LeadToReturn = lead };
        var customerRepo = new FakeCustomerRepository();

        var handler = new ConvertLeadCommandHandler(crmRepo, new FakeActivityRepository(), customerRepo, companyRepo);
        var cmd = new ConvertLeadCommand(companyId, leadId, "C-01");

        // Act
        var result = await handler.HandleAsync(cmd);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("C-01", result.Value.CustomerCode);
        Assert.Equal("OPP-001", result.Value.OpportunityNumber);

        Assert.True(customerRepo.AddCalled);
        Assert.True(crmRepo.AddOpportunityCalled);
        Assert.True(crmRepo.UpdateLeadCalled);
        
        Assert.Equal(LeadStatus.Converted, lead.Status);
    }

    [Fact]
    public async Task HandleAsync_ShouldCopyConversionNote_WhenConverting()
    {
        // Arrange (Block B, task 11.4 audit trail): an assigned lead carries an author.
        var companyId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();

        var companyRepo = new FakeCompanyRepository { CompanyToReturn = new Company { Id = companyId } };
        var lead = new Lead
        {
            Id = leadId,
            CompanyId = companyId,
            Status = LeadStatus.Open,
            LeadCode = "L-42",
            LeadName = "Alex Rivera",
            OrganizationName = "TechCorp",
            AssignedToUserId = assigneeId
        };
        var crmRepo = new FakeCrmRepository { LeadToReturn = lead };
        var activityRepo = new FakeActivityRepository();
        var customerRepo = new FakeCustomerRepository();

        var handler = new ConvertLeadCommandHandler(crmRepo, activityRepo, customerRepo, companyRepo);
        var cmd = new ConvertLeadCommand(companyId, leadId, "C-42");

        // Act
        var result = await handler.HandleAsync(cmd);

        // Assert
        Assert.True(result.IsSuccess);
        var activity = Assert.Single(activityRepo.Activities);
        Assert.Equal(result.Value.OpportunityId, activity.OpportunityId);
        Assert.Equal(CRMActivityType.Note, activity.Type);
        Assert.Equal("Converted from lead L-42", activity.Subject);
        Assert.Contains("L-42", activity.Content!);
        Assert.Contains("Alex Rivera", activity.Content!);
        Assert.Contains("TechCorp", activity.Content!);
        Assert.Equal(assigneeId, activity.CreatedByUserId);
    }

    [Fact]
    public async Task HandleAsync_ShouldPreferCallerAuthor_OverAssignee()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var callerId = Guid.NewGuid();

        var companyRepo = new FakeCompanyRepository { CompanyToReturn = new Company { Id = companyId } };
        var lead = new Lead
        {
            Id = leadId,
            CompanyId = companyId,
            Status = LeadStatus.Open,
            LeadCode = "L-43",
            LeadName = "Sam Lee",
            AssignedToUserId = Guid.NewGuid()
        };
        var crmRepo = new FakeCrmRepository { LeadToReturn = lead };
        var activityRepo = new FakeActivityRepository();

        var handler = new ConvertLeadCommandHandler(crmRepo, activityRepo, new FakeCustomerRepository(), companyRepo);
        var cmd = new ConvertLeadCommand(companyId, leadId, "C-43", ConvertedByUserId: callerId);

        // Act
        var result = await handler.HandleAsync(cmd);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(callerId, Assert.Single(activityRepo.Activities).CreatedByUserId);
    }

    private class FakeCompanyRepository : ICompanyRepository
    {
        public Company CompanyToReturn { get; set; }
        public Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default) => Task.FromResult<Company?>(CompanyToReturn);
        public Task<Company?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<Company?>(CompanyToReturn);
        public Task AddAsync(Company company, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(Company company, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class FakeCrmRepository : ICrmRepository
    {
        public Lead LeadToReturn { get; set; }
        public bool AddOpportunityCalled { get; set; }
        public bool UpdateLeadCalled { get; set; }

        public Task<Lead?> GetLeadByIdAsync(Guid leadId, CancellationToken cancellationToken = default) => Task.FromResult<Lead?>(LeadToReturn);
        public Task<Opportunity?> GetOpportunityByIdAsync(Guid opportunityId, CancellationToken cancellationToken = default) => Task.FromResult<Opportunity?>(null);
        public Task AddLeadAsync(Lead lead, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateLeadAsync(Lead lead, CancellationToken cancellationToken = default) { UpdateLeadCalled = true; return Task.CompletedTask; }
        public Task AddOpportunityAsync(Opportunity opportunity, CancellationToken cancellationToken = default) { AddOpportunityCalled = true; return Task.CompletedTask; }
        public Task UpdateOpportunityAsync(Opportunity opportunity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string> NextOpportunityNumberAsync(Guid companyId, int year, CancellationToken cancellationToken = default) => Task.FromResult("OPP-001");
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) => operation(cancellationToken);
        public Task<Lead?> GetLeadByDedupKeyAsync(Guid companyId, string source, string externalReference, CancellationToken cancellationToken = default) => Task.FromResult<Lead?>(null);
        public Task<System.Collections.Generic.IReadOnlyList<Lead>> ListLeadsAsync(Guid companyId, int limit = 50, CancellationToken cancellationToken = default) => Task.FromResult<System.Collections.Generic.IReadOnlyList<Lead>>(new System.Collections.Generic.List<Lead>());
        public Task<System.Collections.Generic.IReadOnlyList<Opportunity>> ListOpportunitiesAsync(Guid companyId, int limit = 50, CancellationToken cancellationToken = default) => Task.FromResult<System.Collections.Generic.IReadOnlyList<Opportunity>>(new System.Collections.Generic.List<Opportunity>());
    }

    private class FakeActivityRepository : ICrmActivityRepository
    {
        public System.Collections.Generic.List<CRMActivity> Activities { get; } = new();
        public Task AddActivityAsync(CRMActivity activity, CancellationToken cancellationToken = default) { Activities.Add(activity); return Task.CompletedTask; }
        public Task<System.Collections.Generic.IReadOnlyList<CRMActivity>> ListByOpportunityAsync(Guid opportunityId, CancellationToken cancellationToken = default) => Task.FromResult<System.Collections.Generic.IReadOnlyList<CRMActivity>>(Activities.FindAll(a => a.OpportunityId == opportunityId));
    }

    private class FakeCustomerRepository : ICustomerRepository
    {
        public bool AddCalled { get; set; }
        public Task AddAsync(Customer customer, CancellationToken cancellationToken = default) { AddCalled = true; return Task.CompletedTask; }
        public Task<bool> ExistsCodeAsync(Guid companyId, string customerCode, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<Customer?>(null);
        public Task<System.Collections.Generic.IReadOnlyList<Customer>> GetRecentAsync(Guid companyId, int limit, CancellationToken cancellationToken = default) => Task.FromResult<System.Collections.Generic.IReadOnlyList<Customer>>(new System.Collections.Generic.List<Customer>());
        public Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
