using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure C# validation for the Warehouse aggregate (Task 3.1: "Warehouses enforce tree structure").
/// The tree rules deliberately mirror <see cref="AccountValidator"/> (decision D1): a warehouse
/// cannot parent itself, the parent must belong to the same company, only a Group warehouse may
/// have children, and the stored parent chain must not loop.
/// </summary>
public static class WarehouseValidator
{
    public const int MaxCodeLength = 50;
    public const int MaxNameLength = 150;

    /// <summary>Field-level rules: required company / code (50) / name (150) and a linked stock account.</summary>
    /// <exception cref="StockValidationException">An invariant was violated.</exception>
    public static void EnsureValidFields(Guid companyId, string? code, string? name, Guid stockAccountId)
    {
        if (companyId == Guid.Empty)
        {
            throw new StockValidationException(
                StockErrorCodes.WarehouseCompanyRequired,
                "A warehouse must belong to a company (CompanyId is required).");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new StockValidationException(StockErrorCodes.WarehouseCodeRequired, "Warehouse Code is required.");
        }

        if (code.Length > MaxCodeLength)
        {
            throw new StockValidationException(
                StockErrorCodes.WarehouseCodeTooLong,
                $"Warehouse Code must not exceed {MaxCodeLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new StockValidationException(StockErrorCodes.WarehouseNameRequired, "Warehouse Name is required.");
        }

        if (name.Length > MaxNameLength)
        {
            throw new StockValidationException(
                StockErrorCodes.WarehouseNameTooLong,
                $"Warehouse Name must not exceed {MaxNameLength} characters.");
        }

        if (stockAccountId == Guid.Empty)
        {
            throw new StockValidationException(
                StockErrorCodes.MissingStockAccount,
                "A warehouse must be linked to a General Ledger stock account (StockAccountId is required).");
        }
    }

    /// <summary>
    /// Parent rules for a child warehouse (<paramref name="candidate"/>): the parent must exist in
    /// the same company, must be a Group warehouse, and must not be the warehouse itself.
    /// </summary>
    /// <exception cref="StockValidationException">An invariant was violated.</exception>
    public static void EnsureValidParent(Warehouse candidate, Warehouse parent)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(parent);

        if (parent.Id == candidate.Id || candidate.ParentWarehouseId == candidate.Id)
        {
            throw new StockValidationException(StockErrorCodes.ParentIsSelf, "A warehouse cannot be its own parent.");
        }

        if (parent.CompanyId != candidate.CompanyId)
        {
            throw new StockValidationException(
                StockErrorCodes.ParentNotInSameCompany,
                "The parent warehouse must belong to the same company.");
        }

        if (!parent.IsGroup)
        {
            throw new StockValidationException(
                StockErrorCodes.ParentIsNotGroup,
                $"Warehouse '{parent.WarehouseCode}' is not a group warehouse and cannot have children.");
        }
    }

    /// <summary>
    /// Cycle prevention for the parent chain, from the proposed parent up to the root. Needs the
    /// loaded ancestor graph, so the command handler walks it through IWarehouseRepository first
    /// and passes the ids here (same decision C5 pattern as the Account tree).
    /// </summary>
    /// <exception cref="StockValidationException">A cycle was detected.</exception>
    public static void EnsureNoCycle(Guid candidateId, IReadOnlyList<Guid> ancestorIdsFromParentToRoot)
    {
        ArgumentNullException.ThrowIfNull(ancestorIdsFromParentToRoot);

        if (ancestorIdsFromParentToRoot.Contains(candidateId))
        {
            throw new StockValidationException(
                StockErrorCodes.CycleDetected,
                "Cycle detected: the parent chain contains the warehouse itself (it would be its own ancestor).");
        }

        var seen = new HashSet<Guid>();
        foreach (var id in ancestorIdsFromParentToRoot)
        {
            if (!seen.Add(id))
            {
                throw new StockValidationException(
                    StockErrorCodes.CycleDetected,
                    "Cycle detected: the stored parent chain loops back on itself.");
            }
        }
    }
}
