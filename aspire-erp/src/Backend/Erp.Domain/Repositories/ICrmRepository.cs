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

    Task AddLeadAsync(Lead lead, CancellationToken cancellationToken = default);
    Task UpdateLeadAsync(Lead lead, CancellationToken cancellationToken = default);

    Task AddOpportunityAsync(Opportunity opportunity, CancellationToken cancellationToken = default);
    Task UpdateOpportunityAsync(Opportunity opportunity, CancellationToken cancellationToken = default);

    /// <summary>Generates the next sequential opportunity number (e.g., OPP-2026-00001).</summary>
    Task<string> NextOpportunityNumberAsync(Guid companyId, int year, CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}
