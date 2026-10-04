using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure C# field rules for the work order aggregate (Task 9.3). No EF Core, no NuGet packages -
/// Constitution Article I.2 keeps Erp.Domain dependency-free. Mirrors
/// <see cref="PurchaseValidator"/> static-guard style. Status transitions themselves live on
/// <see cref="WorkOrder"/> (JournalEntry state-machine style); this validator covers the
/// creation-time field guards only.
/// </summary>
public static class WorkOrderValidator
{
    /// <summary>Authorized production quantity must be strictly positive (CK_WorkOrder_Quantities).</summary>
    /// <exception cref="ManufacturingValidationException">An invariant was violated.</exception>
    public static void EnsureValidQuantity(decimal quantityToProduce)
    {
        if (quantityToProduce <= 0)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.InvalidWorkOrderQuantity,
                $"Work order quantity to produce must be greater than zero (received {quantityToProduce}).");
        }
    }

    /// <summary>Completed production quantity must be strictly positive and within the authorization.</summary>
    /// <exception cref="ManufacturingValidationException">An invariant was violated.</exception>
    public static void EnsureValidProducedQuantity(decimal producedQuantity, decimal quantityToProduce)
    {
        if (producedQuantity <= 0 || producedQuantity > quantityToProduce)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.InvalidWorkOrderQuantity,
                $"Produced quantity must be greater than zero and within the authorized {quantityToProduce} "
                + $"(received {producedQuantity}). Over-production across multiple manufacture vouchers "
                + "stays deferred (9.6+).");
        }
    }

    /// <summary>Planned end date must not precede the planned start date.</summary>
    /// <exception cref="ManufacturingValidationException">An invariant was violated.</exception>
    public static void EnsureValidDates(DateOnly plannedStartDate, DateOnly plannedEndDate)
    {
        if (plannedEndDate < plannedStartDate)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.InvalidWorkOrderDates,
                $"Planned end date ({plannedEndDate:yyyy-MM-dd}) must not precede planned start date "
                + $"({plannedStartDate:yyyy-MM-dd}).");
        }
    }

    /// <summary>
    /// The two stock movements (Stores -&gt; WIP, WIP -&gt; finished goods) each require a distinct
    /// warehouse pair - a transfer with identical source and target is rejected by the stock engine.
    /// Stores and finished goods may coincide (produce back into Stores); only the movement pairs
    /// must differ.
    /// </summary>
    /// <exception cref="ManufacturingValidationException">An invariant was violated.</exception>
    public static void EnsureValidWarehouses(Guid sourceWarehouseId, Guid wipWarehouseId, Guid targetWarehouseId)
    {
        if (sourceWarehouseId == Guid.Empty || wipWarehouseId == Guid.Empty || targetWarehouseId == Guid.Empty)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.InvalidWorkOrderWarehouses,
                "A work order requires a source (Stores), a WIP transit, and a target (finished goods) warehouse.");
        }

        if (sourceWarehouseId == wipWarehouseId)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.InvalidWorkOrderWarehouses,
                "The source (Stores) and WIP transit warehouses must differ: the MF-02 material transfer moves between them.");
        }

        if (wipWarehouseId == targetWarehouseId)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.InvalidWorkOrderWarehouses,
                "The WIP transit and target (finished goods) warehouses must differ: the MF-03 manufacture "
                + "consumes WIP and receives finished goods across them.");
        }
    }
}
