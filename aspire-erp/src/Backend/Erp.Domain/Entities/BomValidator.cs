using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure C# validation for the BOM aggregate (Task 9.2). No EF Core, no NuGet packages -
/// Constitution Article I.2 keeps Erp.Domain dependency-free. Mirrors <see cref="ItemValidator"/>
/// / <see cref="WarehouseValidator"/> static-guard style.
/// </summary>
/// <remarks>
/// Anti-cycle scope: <see cref="EnsureNoSelfReference"/> guards the DIRECT self-reference
/// (finished item == component) required by Task 9.2 acceptance. Transitive closure across
/// multiple BOMs is Block C/Task 9.7 territory; <see cref="EnsureNoCycle"/> is the plug-in point
/// (same ancestor-id-list shape as <c>WarehouseValidator.EnsureNoCycle</c>) so the closure can
/// land without reshaping this validator.
/// </remarks>
public static class BomValidator
{
    /// <summary>BOM header quantity must be strictly positive (CK_BOM_Quantity).</summary>
    /// <exception cref="ManufacturingValidationException">An invariant was violated.</exception>
    public static void EnsureValidQuantity(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.InvalidBomQuantity,
                $"BOM quantity must be greater than zero (received {quantity}).");
        }
    }

    /// <summary>A BOM with zero component lines is meaningless - reject it.</summary>
    /// <exception cref="ManufacturingValidationException">The BOM has no items.</exception>
    public static void EnsureHasItems(IReadOnlyCollection<BomItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.EmptyBom,
                "A BOM must declare at least one component item.");
        }
    }

    /// <summary>
    /// DIRECT anti-cycle guard (Task 9.2 acceptance): the finished <paramref name="finishedItemId"/>
    /// must not appear among its own <paramref name="items"/> components.
    /// </summary>
    /// <exception cref="ManufacturingValidationException">A direct self-reference was found.</exception>
    public static void EnsureNoSelfReference(Guid finishedItemId, IEnumerable<BomItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        foreach (var item in items)
        {
            if (item.ItemId == finishedItemId)
            {
                throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.CircularReference,
                    "Circular reference: the finished item cannot be a component of its own BOM.");
            }
        }
    }

    /// <summary>
    /// Transitive-cycle plug-in point for Task 9.7 (Block C): <paramref name="ancestorItemIds"/> is
    /// the finished-item chain from the parent BOM up to the root (loaded by the caller, same
    /// decision-C5 pattern as the Account/Warehouse tree walks). A finished item that already
    /// appears in its own ancestor chain would close a multi-level loop.
    /// </summary>
    /// <exception cref="ManufacturingValidationException">A transitive cycle was detected.</exception>
    public static void EnsureNoCycle(Guid finishedItemId, IReadOnlyList<Guid> ancestorItemIds)
    {
        ArgumentNullException.ThrowIfNull(ancestorItemIds);

        if (ancestorItemIds.Contains(finishedItemId))
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.CircularReference,
                "Circular reference: the finished item already appears in its own BOM ancestor chain.");
        }

        var seen = new HashSet<Guid>();
        foreach (var id in ancestorItemIds)
        {
            if (!seen.Add(id))
            {
                throw new ManufacturingValidationException(
                    ManufacturingErrorCodes.CircularReference,
                    "Circular reference: the BOM ancestor chain loops back on itself.");
            }
        }
    }

    /// <summary>
    /// Component-line rules: quantity &gt; 0, valuation rate &gt;= 0, amount &gt;= 0,
    /// scrap percentage &gt;= 0 (values above 100 stay legal - ERPNext multi-output scrap).
    /// </summary>
    /// <exception cref="ManufacturingValidationException">An invariant was violated.</exception>
    public static void EnsureValidItem(BomItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Quantity <= 0)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.InvalidBomItemQuantity,
                $"BOM item quantity must be greater than zero (received {item.Quantity}).");
        }

        if (item.ValuationRate < 0)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.NegativeValuationRate,
                $"BOM item valuation rate must not be negative (received {item.ValuationRate}).");
        }

        if (item.Amount < 0)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.NegativeBomAmount,
                $"BOM item amount must not be negative (received {item.Amount}).");
        }

        if (item.ScrapPercentage < 0)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.NegativeScrapPercentage,
                $"BOM item scrap percentage must not be negative (received {item.ScrapPercentage}).");
        }
    }

    /// <summary>Operation rules: duration must be strictly positive (workstation resolution is Block B).</summary>
    /// <exception cref="ManufacturingValidationException">An invariant was violated.</exception>
    public static void EnsureValidOperation(BomOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.DurationMinutes <= 0)
        {
            throw new ManufacturingValidationException(
                ManufacturingErrorCodes.InvalidOperationDuration,
                $"BOM operation duration must be greater than zero minutes (received {operation.DurationMinutes}).");
        }
    }
}
