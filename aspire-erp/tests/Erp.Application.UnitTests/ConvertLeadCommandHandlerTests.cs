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

        var handler = new ConvertLeadCommandHandler(crmRepo, customerRepo, companyRepo);
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
