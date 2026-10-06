using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the manufacturing masters (Tasks 9.1-9.2): workstation and BOM
/// persistence - plus the work-order workflow and gapless numbering of Block B (Tasks 9.3/9.4).
/// Implemented by Erp.Infrastructure.Data.Repositories.ManufacturingRepository.
/// </summary>
/// <remarks>
/// All writes happen through <see cref="ExecuteInTransactionAsync{T}"/> so the voucher number,
/// the work-order mutation, its StockLedgerEntry rows and its GLEntry rows commit atomically.
/// The ambient-transaction join mirrors <see cref="IStockRepository"/>: both repositories share
/// the same scoped AppDbContext, so the manufacture posting nests instead of dead-locking.
/// </remarks>
public interface IManufacturingRepository
{
    /// <summary>
    /// Runs <paramref name="operation"/> inside ONE database transaction and commits only when it
    /// returns; any exception rolls the whole posting back. Joins an already-open transaction so
    /// nested calls compose instead of dead-locking (same contract as IStockRepository).
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Next gapless work-order number, e.g. prefix "WO" + 2026 -&gt; "WO-2026-00001" (ambient
    /// transaction required - SELECT MAX(OrderNumber) WITH (UPDLOCK, HOLDLOCK)).
    /// </summary>
    Task<string> NextWorkOrderNumberAsync(Guid companyId, string prefix, int year, CancellationToken cancellationToken = default);

    /// <summary>Gets a workstation by its ID.</summary>
    Task<Workstation?> GetWorkstationByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets a BOM by its ID, including its items and operations.</summary>
    Task<BillOfMaterials?> GetBomByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The default active recipe producing <paramref name="itemId"/> (Task 9.7 transitive-cycle
    /// walk at work-order submit). Null when the component is a purchased leaf with no recipe.
    /// When several default active BOMs exist, the first one wins (uniqueness unenforced).
    /// </summary>
    Task<BillOfMaterials?> GetDefaultActiveBomByItemIdAsync(Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>Lists one company's BOM headers with their items and operations (Task 9.5 UI reads).</summary>
    Task<PagedResult<BillOfMaterials>> ListBomsAsync(Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default);

    /// <summary>Lists one company's work-order headers (Task 9.5 execution board reads).</summary>
    Task<PagedResult<WorkOrder>> ListWorkOrdersAsync(Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default);

    /// <summary>Gets a work order by its ID (header only - the aggregate carries no lines).</summary>
    Task<WorkOrder?> GetWorkOrderByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Persists a new workstation.</summary>
    Task AddWorkstationAsync(Workstation workstation, CancellationToken cancellationToken = default);

    /// <summary>Persists a new BOM with its items and operations.</summary>
    Task AddBomAsync(BillOfMaterials bom, CancellationToken cancellationToken = default);

    /// <summary>Persists a new work order in Draft (inside the ambient creation transaction).</summary>
    Task AddWorkOrderAsync(WorkOrder workOrder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves workflow status transitions of an already-tracked work order (inside the ambient
    /// transaction). Translates the RowVersion mismatch into <see cref="ConcurrencyConflictException"/>
    /// like the purchase repository (spec MF-06 optimistic-locking half).
    /// </summary>
    Task UpdateWorkOrderAsync(WorkOrder workOrder, CancellationToken cancellationToken = default);
}
