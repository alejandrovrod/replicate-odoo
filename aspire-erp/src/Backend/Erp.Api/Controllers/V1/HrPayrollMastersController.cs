using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.HrPayroll.Queries;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// HR master reads (Task 12.5): employees, salary components, salary structures and structure
/// assignments for one company. Read-only - all four write no GLEntry rows, so no
/// <c>Idempotency-Key</c> filter applies.
/// </summary>
/// <remarks>
/// SCOPE (documented, the manufacturing-BOM precedent): masters enter via SQL seeds like
/// BOMs (out of scope) - there are deliberately NO create/update endpoints here. These four
/// GET routes exist only so the HR Directory &amp; Payroll Studio UI renders live masters
/// instead of pasted GUIDs.
/// </remarks>
[ApiController]
[Route("api/v1/hr")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
public sealed class HrPayrollMastersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public HrPayrollMastersController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Lists the company's employees, ordered by employee number. Read-only.</summary>
    /// <param name="companyId">Company that owns the employees.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("employees")]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Employees(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidCompany"),
                _errors.Text(HrPayrollErrorCodes.CompanyNotFound),
                HrPayrollErrorCodes.CompanyNotFound);
        }

        var employees = await _sender.SendAsync(new GetEmployeesQuery(companyId), cancellationToken);
        return Ok(employees);
    }

    /// <summary>Lists the company's salary components, ordered by name. Read-only.</summary>
    /// <param name="companyId">Company that owns the components.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("salary-components")]
    [ProducesResponseType(typeof(IReadOnlyList<SalaryComponentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Components(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidCompany"),
                _errors.Text(HrPayrollErrorCodes.CompanyNotFound),
                HrPayrollErrorCodes.CompanyNotFound);
        }

        var components = await _sender.SendAsync(new GetSalaryComponentsQuery(companyId), cancellationToken);
        return Ok(components);
    }

    /// <summary>Lists the company's salary structures with their priced lines. Read-only.</summary>
    /// <param name="companyId">Company that owns the structures.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("salary-structures")]
    [ProducesResponseType(typeof(IReadOnlyList<SalaryStructureDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Structures(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidCompany"),
                _errors.Text(HrPayrollErrorCodes.CompanyNotFound),
                HrPayrollErrorCodes.CompanyNotFound);
        }

        var structures = await _sender.SendAsync(new GetSalaryStructuresQuery(companyId), cancellationToken);
        return Ok(structures);
    }

    /// <summary>Lists the company's structure assignments (eligibility windows). Read-only.</summary>
    /// <param name="companyId">Company that owns the assignments.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("structure-assignments")]
    [ProducesResponseType(typeof(IReadOnlyList<SalaryStructureAssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Assignments(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidCompany"),
                _errors.Text(HrPayrollErrorCodes.CompanyNotFound),
                HrPayrollErrorCodes.CompanyNotFound);
        }

        var assignments = await _sender.SendAsync(new GetStructureAssignmentsQuery(companyId), cancellationToken);
        return Ok(assignments);
    }

    private ObjectResult Problem(int status, string title, string detail, string? code)
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = HttpContext.Request.Path.Value,
        };

        if (code is not null)
        {
            problem.Extensions["code"] = code;
        }

        return new ObjectResult(problem) { StatusCode = status };
    }
}
