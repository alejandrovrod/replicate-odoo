using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure C# validation for the Department tree (Task 12.1). Mirrors the
/// <see cref="WarehouseValidator"/> SHAPE (not its group rule - departments have no IsGroup
/// gate): a department cannot parent itself, the parent must belong to the same company, and
/// the stored parent chain must not loop. The handler walks the ancestor graph through
/// IHrPayrollRepository first and passes the ids here.
/// </summary>
public static class DepartmentValidator
{
    public const int MaxNameLength = 100;

    /// <summary>Field-level rules: required company / name (max 100).</summary>
    /// <exception cref="HrValidationException">An invariant was violated.</exception>
    public static void EnsureValidFields(Guid companyId, string? name)
    {
        if (companyId == Guid.Empty)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.DepartmentCompanyRequired,
                "A department must belong to a company (CompanyId is required).");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.DepartmentNameRequired,
                "Department Name is required.");
        }

        if (name.Length > MaxNameLength)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.DepartmentNameTooLong,
                $"Department Name must not exceed {MaxNameLength} characters.");
        }
    }

    /// <summary>
    /// Parent rules for a child department (<paramref name="candidate"/>): the parent must exist
    /// in the same company and must not be the department itself.
    /// </summary>
    /// <exception cref="HrValidationException">An invariant was violated.</exception>
    public static void EnsureValidParent(Department candidate, Department parent)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(parent);

        if (parent.Id == candidate.Id || candidate.ParentDepartmentId == candidate.Id)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.ParentIsSelf,
                "A department cannot be its own parent.");
        }

        if (parent.CompanyId != candidate.CompanyId)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.ParentNotInSameCompany,
                "The parent department must belong to the same company.");
        }
    }

    /// <summary>
    /// Cycle prevention for the parent chain, from the proposed parent up to the root. Same
    /// ancestor-walk shape as <see cref="WarehouseValidator.EnsureNoCycle"/>.
    /// </summary>
    /// <exception cref="HrValidationException">A cycle was detected.</exception>
    public static void EnsureNoCycle(Guid candidateId, IReadOnlyList<Guid> ancestorIdsFromParentToRoot)
    {
        ArgumentNullException.ThrowIfNull(ancestorIdsFromParentToRoot);

        if (ancestorIdsFromParentToRoot.Contains(candidateId))
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.CycleDetected,
                "Cycle detected: the parent chain contains the department itself (it would be its own ancestor).");
        }

        var seen = new HashSet<Guid>();
        foreach (var id in ancestorIdsFromParentToRoot)
        {
            if (!seen.Add(id))
            {
                throw new HrValidationException(
                    HrPayrollErrorCodes.CycleDetected,
                    "Cycle detected: the stored parent chain loops back on itself.");
            }
        }
    }
}
