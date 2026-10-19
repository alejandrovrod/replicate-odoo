using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="ICrmRepository"/> for the CRM-parity suites (detail/update/
/// quotation): opportunities live as object graphs, so navigations populated by the test
/// read back exactly as the EF <c>Include</c> chain would materialize them.
/// </summary>
public sealed class FakeCrmParityRepository : ICrmRepository
{
    private readonly List<Lead> _leads = new();
    private readonly List<Opportunity> _opportunities = new();

    public IReadOnlyList<Opportunity> Opportunities => _opportunities;

    public void SeedLead(Lead lead) => _leads.Add(lead);

    public void SeedOpportunity(Opportunity opportunity) => _opportunities.Add(opportunity);

    public Task<Lead?> GetLeadByIdAsync(Guid leadId, CancellationToken cancellationToken = default)
        => Task.FromResult(_leads.FirstOrDefault(l => l.Id == leadId));

    public Task<Opportunity?> GetOpportunityByIdAsync(Guid opportunityId, CancellationToken cancellationToken = default)
        => Task.FromResult(_opportunities.FirstOrDefault(o => o.Id == opportunityId));

    public Task<Lead?> GetLeadByDedupKeyAsync(Guid companyId, string source, string externalReference, CancellationToken cancellationToken = default)
        => Task.FromResult(_leads.FirstOrDefault(l =>
            l.CompanyId == companyId && l.Source == source && l.ExternalReference == externalReference));

    public Task<PagedResult<Lead>> ListLeadsAsync(Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default)
    {
        var items = _leads.Where(l => l.CompanyId == companyId).ToList();
        return Task.FromResult(new PagedResult<Lead>(items, items.Count, paging.SafePageNumber, paging.SafePageSize));
    }

    public Task<PagedResult<Opportunity>> ListOpportunitiesAsync(Guid companyId, PagedRequest paging, string? stage, CancellationToken cancellationToken = default)
    {
        var items = _opportunities.Where(o => o.CompanyId == companyId).ToList();
        return Task.FromResult(new PagedResult<Opportunity>(items, items.Count, paging.SafePageNumber, paging.SafePageSize));
    }

    public Task AddLeadAsync(Lead lead, CancellationToken cancellationToken = default)
    {
        _leads.Add(lead);
        return Task.CompletedTask;
    }

    public Task UpdateLeadAsync(Lead lead, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task AddOpportunityAsync(Opportunity opportunity, CancellationToken cancellationToken = default)
    {
        _opportunities.Add(opportunity);
        return Task.CompletedTask;
    }

    public Task UpdateOpportunityAsync(Opportunity opportunity, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ReplaceOpportunityItemsAsync(
        Guid opportunityId, IReadOnlyList<OpportunityItem> lines, CancellationToken cancellationToken = default)
    {
        var existing = _opportunities.FirstOrDefault(o => o.Id == opportunityId);
        if (existing is not null)
        {
            existing.Items.Clear();
            foreach (var line in lines)
            {
                existing.Items.Add(line);
            }
        }

        return Task.CompletedTask;
    }

    public Task<string> NextOpportunityNumberAsync(Guid companyId, int year, CancellationToken cancellationToken = default)
        => Task.FromResult($"OPP-{year}-{_opportunities.Count + 1:D5}");

    public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        => operation(cancellationToken);
}

/// <summary>In-memory <see cref="ICrmActivityRepository"/> (captures follow-up notes).</summary>
public sealed class FakeCrmActivityRepository : ICrmActivityRepository
{
    public List<CRMActivity> Activities { get; } = new();

    public Task AddActivityAsync(CRMActivity activity, CancellationToken cancellationToken = default)
    {
        Activities.Add(activity);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CRMActivity>> ListByOpportunityAsync(
        Guid opportunityId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CRMActivity>>(
            Activities.Where(a => a.OpportunityId == opportunityId).ToList());
}

/// <summary>In-memory <see cref="IQuotationRepository"/> (quotation issue + history reads).</summary>
public sealed class FakeQuotationRepository : IQuotationRepository
{
    private readonly List<Quotation> _quotations = new();

    public IReadOnlyList<Quotation> Quotations => _quotations;

    public void Seed(Quotation quotation) => _quotations.Add(quotation);

    public Task<Quotation?> GetByIdAsync(Guid quotationId, CancellationToken cancellationToken = default)
        => Task.FromResult(_quotations.FirstOrDefault(q => q.Id == quotationId));

    public Task AddAsync(Quotation quotation, CancellationToken cancellationToken = default)
    {
        _quotations.Add(quotation);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Quotation>> ListQuotationsByOpportunityAsync(
        Guid opportunityId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Quotation>>(
            _quotations.Where(q => q.OpportunityId == opportunityId).ToList());

    public Task<string> NextQuotationNumberAsync(Guid companyId, int year, CancellationToken cancellationToken = default)
        => Task.FromResult($"QTN-{year}-{_quotations.Count + 1:D5}");
}

/// <summary>In-memory <see cref="ICrmCatalogRepository"/> (seeded segmentation catalogs).</summary>
public sealed class FakeCrmCatalogRepository : ICrmCatalogRepository
{
    private readonly List<OpportunityLossReason> _reasons = new();
    private readonly List<Territory> _territories = new();
    private readonly List<Industry> _industries = new();

    public void SeedReasons(params OpportunityLossReason[] reasons) => _reasons.AddRange(reasons);

    public void SeedTerritories(params Territory[] territories) => _territories.AddRange(territories);

    public void SeedIndustries(params Industry[] industries) => _industries.AddRange(industries);

    public Task<IReadOnlyList<OpportunityLossReason>> ListLossReasonsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<OpportunityLossReason>>(
            _reasons.Where(r => r.IsActive).OrderBy(r => r.Name).ToList());

    public Task<OpportunityLossReason?> GetLossReasonByIdAsync(Guid lossReasonId, CancellationToken cancellationToken = default)
        => Task.FromResult(_reasons.FirstOrDefault(r => r.Id == lossReasonId));

    public Task<IReadOnlyList<Territory>> ListTerritoriesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Territory>>(
            _territories.Where(t => t.IsActive).OrderBy(t => t.Name).ToList());

    public Task<IReadOnlyList<Industry>> ListIndustriesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Industry>>(
            _industries.Where(t => t.IsActive).OrderBy(t => t.Name).ToList());
}
