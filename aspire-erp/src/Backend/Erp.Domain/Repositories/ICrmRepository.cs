using Erp.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for CRM entities (Leads, Opportunities, etc).
/// </summary>
public interface ICrmRepository
{
    Task<Lead?> GetLeadByIdAsync(Guid leadId, CancellationToken cancellationToken = default);
    Task<Opportunity?> GetOpportunityByIdAsync(Guid opportunityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Webhook replay lookup (Block B, spec CRM-04): the lead carrying this external reference
    /// for the company/source, or null. Implemented by EF and every test fake.
    /// </summary>
    Task<Lead?> GetLeadByDedupKeyAsync(Guid companyId, string source, string externalReference, CancellationToken cancellationToken = default);

    /// <summary>Most recent leads of a company (the Block B list read).</summary>
    Task<IReadOnlyList<Lead>> ListLeadsAsync(Guid companyId, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>Most recent opportunities of a company (the Block B board/list read).</summary>
    Task<IReadOnlyList<Opportunity>> ListOpportunitiesAsync(Guid companyId, int limit = 50, CancellationToken cancellationToken = default);

    Task AddLeadAsync(Lead lead, CancellationToken cancellationToken = default);
    Task UpdateLeadAsync(Lead lead, CancellationToken cancellationToken = default);

    Task AddOpportunityAsync(Opportunity opportunity, CancellationToken cancellationToken = default);
    Task UpdateOpportunityAsync(Opportunity opportunity, CancellationToken cancellationToken = default);

    /// <summary>Generates the next sequential opportunity number (e.g., OPP-2026-00001).</summary>
    Task<string> NextOpportunityNumberAsync(Guid companyId, int year, CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}
