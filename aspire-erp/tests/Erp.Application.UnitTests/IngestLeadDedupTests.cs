using Erp.Application.Features.Crm.Commands;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Erp.Application.UnitTests.Crm;

/// <summary>
/// Block B tests for idempotent webhook intake (task 11.6, spec CRM-04): replaying a
/// duplicate (CompanyId, Source, DeduplicationKey) returns the existing lead with
/// <c>Duplicate=true</c> and zero new rows; a fresh key (or none) inserts.
/// </summary>
public class IngestLeadDedupTests
{
    [Fact]
    public async Task HandleAsync_ShouldReturnExistingLead_WithoutInsert_OnDuplicateKey()
    {
        var companyId = Guid.NewGuid();
        var existing = new Lead
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            LeadCode = "L-WEB-99",
            LeadName = "Alex Rivera",
            Source = "Website",
            ExternalReference = "idemp-lead-2026-99",
            Status = LeadStatus.Open
        };
        var repo = new FakeCrmRepository { DedupMatch = existing };
        var handler = new IngestLeadCommandHandler(repo, FakeCompany(companyId));

        var result = await handler.HandleAsync(
            new IngestLeadCommand(companyId, "L-WEB-99-B", "Someone Else", Source: "Website", DeduplicationKey: "idemp-lead-2026-99"));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Duplicate);
        Assert.Equal(existing.Id, result.Value.Lead.Id);
        Assert.Equal("L-WEB-99", result.Value.Lead.LeadCode);
        Assert.Empty(repo.AddedLeads);
    }

    [Fact]
    public async Task HandleAsync_ShouldInsert_WhenDedupKeyIsNew()
    {
        var companyId = Guid.NewGuid();
        var repo = new FakeCrmRepository { DedupMatch = null };
        var handler = new IngestLeadCommandHandler(repo, FakeCompany(companyId));

        var result = await handler.HandleAsync(
            new IngestLeadCommand(companyId, "L-WEB-100", "Alex Rivera", Source: "Website", DeduplicationKey: "idemp-lead-2026-100"));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Duplicate);
        var inserted = Assert.Single(repo.AddedLeads);
        Assert.Equal(result.Value.Lead.Id, inserted.Id);
        Assert.Equal("idemp-lead-2026-100", inserted.ExternalReference);
    }

    [Fact]
    public async Task HandleAsync_ShouldAlwaysInsert_WhenNoDedupKey()
    {
        var companyId = Guid.NewGuid();
        var repo = new FakeCrmRepository();
        var handler = new IngestLeadCommandHandler(repo, FakeCompany(companyId));

        var first = await handler.HandleAsync(new IngestLeadCommand(companyId, "L-1", "Alex Rivera"));
        var second = await handler.HandleAsync(new IngestLeadCommand(companyId, "L-2", "Alex Rivera"));

        Assert.True(first.IsSuccess && second.IsSuccess);
        Assert.False(first.Value!.Duplicate || second.Value!.Duplicate);
        Assert.NotEqual(first.Value.Lead.Id, second.Value.Lead.Id);
        Assert.Equal(2, repo.AddedLeads.Count);
        Assert.All(repo.AddedLeads, l => Assert.Null(l.ExternalReference));
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectUnknownCompany_BeforeDedupLookup()
    {
        var repo = new FakeCrmRepository();
        var handler = new IngestLeadCommandHandler(repo, new FakeCompanyRepository { CompanyToReturn = null });

        var result = await handler.HandleAsync(
            new IngestLeadCommand(Guid.NewGuid(), "L-1", "Alex Rivera", DeduplicationKey: "k-1"));

        Assert.False(result.IsSuccess);
        Assert.Equal(CRMErrorCodes.CompanyRequired, result.Error!.Code);
        Assert.Null(repo.LastDedupLookup);
        Assert.Empty(repo.AddedLeads);
    }

    private static FakeCompanyRepository FakeCompany(Guid companyId) =>
        new() { CompanyToReturn = new Company { Id = companyId } };

    private sealed class FakeCompanyRepository : ICompanyRepository
    {
        public Company? CompanyToReturn { get; set; }
        public Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default) => Task.FromResult(CompanyToReturn);
        public Task<Company?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(CompanyToReturn);
        public Task AddAsync(Company company, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(Company company, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeCrmRepository : ICrmRepository
    {
        public Lead? DedupMatch { get; set; }
        public (Guid CompanyId, string Source, string Key)? LastDedupLookup { get; private set; }
        public System.Collections.Generic.List<Lead> AddedLeads { get; } = new();

        public Task<Lead?> GetLeadByIdAsync(Guid leadId, CancellationToken cancellationToken = default) => Task.FromResult<Lead?>(null);
        public Task<Opportunity?> GetOpportunityByIdAsync(Guid opportunityId, CancellationToken cancellationToken = default) => Task.FromResult<Opportunity?>(null);
        public Task AddLeadAsync(Lead lead, CancellationToken cancellationToken = default) { AddedLeads.Add(lead); return Task.CompletedTask; }
        public Task UpdateLeadAsync(Lead lead, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddOpportunityAsync(Opportunity opportunity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateOpportunityAsync(Opportunity opportunity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string> NextOpportunityNumberAsync(Guid companyId, int year, CancellationToken cancellationToken = default) => Task.FromResult("OPP-1");
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) => operation(cancellationToken);
        public Task<Lead?> GetLeadByDedupKeyAsync(Guid companyId, string source, string externalReference, CancellationToken cancellationToken = default)
        {
            LastDedupLookup = (companyId, source, externalReference);
            return Task.FromResult(DedupMatch);
        }
        public Task<PagedResult<Lead>> ListLeadsAsync(Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<Lead>(new List<Lead>(), 0, paging.SafePageNumber, paging.SafePageSize));
        public Task<PagedResult<Opportunity>> ListOpportunitiesAsync(Guid companyId, PagedRequest paging, string? stage, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<Opportunity>(new List<Opportunity>(), 0, paging.SafePageNumber, paging.SafePageSize));
    }
}
