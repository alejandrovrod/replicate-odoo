using Erp.Domain.Common;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Production authorization workflow (Task 9.3, plan.md §1 DDL table 4):
/// Draft -&gt; Submitted -&gt; InProcess -&gt; Completed, with Cancelled reachable from every
/// non-terminal state for the Block C (Task 9.6) reversal to drive. Persisted Status follows the
/// PurchaseOrder precedent (integer conversion).
/// </summary>
public enum WorkOrderStatus
{
    Draft,
    Submitted,
    InProcess,
    Completed,
    Cancelled
}

/// <summary>
/// A work order header (Task 9.3): authorizes production of <see cref="QuantityToProduce"/>
/// units of <see cref="ProductionItemId"/> from <see cref="BomId"/>, moving components
/// Stores (<see cref="SourceWarehouseId"/>) -&gt; WIP (<see cref="WipWarehouseId"/>) and
/// receiving finished goods into <see cref="TargetWarehouseId"/> on completion (Task 9.4).
/// </summary>
/// <remarks>
/// Company-scoped: the order belongs to the company that owns its three warehouses.
/// The gapless <see cref="OrderNumber"/> (WO-YYYY-NNNNN) is assigned inside the creation
/// transaction, mirroring the purchase-order precedent. Partial production is tracked through
/// <see cref="ProducedQuantity"/>; multi-voucher partial completion stays deferred (9.6+).
/// </remarks>
public class WorkOrder : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    /// <summary>Gapless voucher number (Constitution III.4): WO-2026-00001, assigned at creation.</summary>
    public string OrderNumber { get; set; } = string.Empty;

    public Guid ProductionItemId { get; set; }

    public Item? ProductionItem { get; set; }

    public Guid BomId { get; set; }

    public BillOfMaterials? Bom { get; set; }

    /// <summary>Authorized production quantity (decimal(18,4), strictly positive).</summary>
    public decimal QuantityToProduce { get; set; }

    /// <summary>Finished quantity received so far (decimal(18,4), &gt;= 0).</summary>
    public decimal ProducedQuantity { get; set; }

    /// <summary>Workflow state (Task 9.3 acceptance: Draft -&gt; Submitted -&gt; InProcess -&gt; Completed).</summary>
    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Draft;

    /// <summary>Stores warehouse components are issued from (MF-02 source).</summary>
    public Guid SourceWarehouseId { get; set; }

    public Warehouse? SourceWarehouse { get; set; }

    /// <summary>Transit warehouse holding components under transformation (MF-02 target, MF-03 source).</summary>
    public Guid WipWarehouseId { get; set; }

    public Warehouse? WipWarehouse { get; set; }

    /// <summary>Finished-goods warehouse completed units are received into (MF-03 target).</summary>
    public Guid TargetWarehouseId { get; set; }

    public Warehouse? TargetWarehouse { get; set; }

    public DateOnly PlannedStartDate { get; set; }

    public DateOnly PlannedEndDate { get; set; }

    public DateOnly? ActualStartDate { get; set; }

    public DateOnly? ActualEndDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Optimistic concurrency token (SQL Server <c>rowversion</c> - spec MF-06): the workflow
    /// (Draft -&gt; Submitted -&gt; InProcess -&gt; Completed) is a read-modify-write, so EF puts the
    /// original value in the UPDATE ... WHERE clause and a concurrent transition between the load
    /// and the save throws <c>DbUpdateConcurrencyException</c> instead of being silently lost.
    /// Store-generated: never set from code.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    /// <summary>
    /// Draft -&gt; Submitted (Task 9.3 acceptance). The submit handler verifies the referenced BOM
    /// is active and default BEFORE calling this - a rejected submission never reaches the
    /// transition and never writes a row.
    /// </summary>
    /// <exception cref="ManufacturingValidationException">The order is not a Draft (<c>invalid_status_transition</c>).</exception>
    public void Submit()
    {
        if (Status != WorkOrderStatus.Draft)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.InvalidStatusTransition,
                $"Only a Draft work order can be submitted; order '{OrderNumber}' is '{Status}'.");
        }

        Status = WorkOrderStatus.Submitted;
    }

    /// <summary>
    /// Submitted -&gt; InProcess: components left Stores for WIP (MF-02 transfer posted). Called
    /// by the transfer handler only after the stock posting succeeds.
    /// </summary>
    /// <exception cref="ManufacturingValidationException">The order is not Submitted (<c>invalid_status_transition</c>).</exception>
    public void StartProduction()
    {
        if (Status != WorkOrderStatus.Submitted)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.InvalidStatusTransition,
                $"Production can start only from a Submitted work order; order '{OrderNumber}' is '{Status}'.");
        }

        Status = WorkOrderStatus.InProcess;
    }

    /// <summary>
    /// InProcess -&gt; Completed: finished goods were received (MF-03 manufacture posted). Called
    /// by the completion handler only after the manufacture voucher succeeds.
    /// </summary>
    /// <exception cref="ManufacturingValidationException">The order is not InProcess (<c>invalid_status_transition</c>).</exception>
    public void Complete()
    {
        if (Status != WorkOrderStatus.InProcess)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.InvalidStatusTransition,
                $"Only an InProcess work order can be completed; order '{OrderNumber}' is '{Status}'.");
        }

        Status = WorkOrderStatus.Completed;
    }

    /// <summary>
    /// Draft/Submitted/InProcess -&gt; Cancelled. Exists so the Block C (Task 9.6) cancellation
    /// handler has a transition to drive; terminal states (Completed, Cancelled) are guarded.
    /// </summary>
    /// <exception cref="ManufacturingValidationException">The order is already terminal (<c>invalid_status_transition</c>).</exception>
    public void Cancel()
    {
        if (Status is WorkOrderStatus.Completed or WorkOrderStatus.Cancelled)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.InvalidStatusTransition,
                $"A terminal work order cannot be cancelled; order '{OrderNumber}' is '{Status}'.");
        }

        Status = WorkOrderStatus.Cancelled;
    }
}
