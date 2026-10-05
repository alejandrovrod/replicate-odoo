using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.HrPayroll.Commands;

/// <summary>One priced component row of a <see cref="CreateSalaryStructureCommand"/>.</summary>
/// <param name="ComponentId">The salary component priced by this line.</param>
/// <param name="Amount">Fixed money part; must be &gt;= 0.</param>
/// <param name="PercentageOfBase">Share of the base-earnings total in percent; null means none; must be &gt;= 0 when set.</param>
public sealed record SalaryStructureLineInput(
    Guid ComponentId,
    decimal Amount,
    decimal? PercentageOfBase = null);

/// <summary>
/// Creates one salary structure with its component lines (Task 12.2): every referenced
/// component must exist, be active and belong to the same company. Header + lines persist
/// atomically (ONE transaction). A rejected creation writes zero rows.
/// </summary>
public sealed record CreateSalaryStructureCommand(
    Guid CompanyId,
    string StructureName,
    IReadOnlyList<SalaryStructureLineInput> Lines) : ICommand<Result<SalaryStructureDto>>;
