using Erp.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for formal quotations (CRM/ERPNext parity): the pre-order flow
/// issued from a ClosedWon deal. Implemented by <see cref="Infrastructure"/> CrmRepository
/// through the same scoped DbContext (the dual-contract precedent).
/// </summary>
public interface IQuotationRepository
{
    Task<Quotation?> GetByIdAsync(Guid quotationId, CancellationToken cancellationToken = default);
    Task AddAsync(Quotation quotation, CancellationToken cancellationToken = default);

    /// <summary>Quotations issued from one deal, newest first (detail-dialog history).</summary>
    Task<IReadOnlyList<Quotation>> ListQuotationsByOpportunityAsync(
        Guid opportunityId, CancellationToken cancellationToken = default);

    /// <summary>Next gapless quotation number, e.g. 2026 -> "QTN-2026-00001".</summary>
    Task<string> NextQuotationNumberAsync(Guid companyId, int year, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only CRM segmentation catalogs (loss reasons, territories, industries): seeded
/// rows listed for the detail/close dropdowns. No write surface by design.
/// </summary>
public interface ICrmCatalogRepository
{
    Task<IReadOnlyList<OpportunityLossReason>> ListLossReasonsAsync(
        CancellationToken cancellationToken = default);
    Task<OpportunityLossReason?> GetLossReasonByIdAsync(
        Guid lossReasonId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Territory>> ListTerritoriesAsync(
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Industry>> ListIndustriesAsync(
        CancellationToken cancellationToken = default);
}
