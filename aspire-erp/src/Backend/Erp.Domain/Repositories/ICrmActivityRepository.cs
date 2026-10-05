using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the CRM follow-up log (plan.md §1 CRMActivity).
/// </summary>
/// <remarks>
/// Kept as a SEPARATE contract from <see cref="ICrmRepository"/> on purpose: the adopted
/// Block A handler tests ship their own private <c>ICrmRepository</c> fakes, so extending
/// that interface would break their compilation. One EF class (<c>CrmRepository</c>)
/// implements both contracts - the SalesRepository (ISalesOrderRepository +
/// IDeliveryNoteRepository) precedent - so Block B still writes through a single context.
/// </remarks>
public interface ICrmActivityRepository
{
    /// <summary>Persists one follow-up log entry against its opportunity.</summary>
    Task AddActivityAsync(CRMActivity activity, CancellationToken cancellationToken = default);

    /// <summary>Follow-up log of one opportunity, oldest first (the audit trail CRM-03 needs).</summary>
    Task<IReadOnlyList<CRMActivity>> ListByOpportunityAsync(Guid opportunityId, CancellationToken cancellationToken = default);
}
