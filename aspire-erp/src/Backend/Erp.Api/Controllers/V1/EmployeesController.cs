using Erp.Api.Common;
using Erp.Api.Filters;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.HrPayroll.Commands;
using Erp.Application.Features.HrPayroll.Queries;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>Create-employee body: identity, dates, pay mode/status, optional bank data and the optional initial structure.</summary>
public sealed record CreateEmployeeRequest(
    Guid CompanyId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string WorkEmail,
    DateOnly DateOfJoining,
    DateOnly? DateOfRelieving,
    SalaryMode SalaryMode,
    string? BankName,
    string? BankAccountNumber,
    EmploymentStatus Status,
    Guid? SalaryStructureId);

/// <summary>Update-employee body: same fields plus the optimistic <c>rowVersion</c> token (base64) echoed from the read.</summary>
public sealed record UpdateEmployeeRequest(
    Guid CompanyId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string WorkEmail,
    DateOnly DateOfJoining,
    DateOnly? DateOfRelieving,
    SalaryMode SalaryMode,
    string? BankName,
    string? BankAccountNumber,
    EmploymentStatus Status,
    byte[]? RowVersion);

/// <summary>
/// Employee master endpoints: list, register and edit. The directory read used to live on
/// HrPayrollMastersController (read-only masters) - employees moved here now that they carry a
/// lifecycle with writes (Constitution Article VI: explicit route + versioning + TenantMember
/// policy, exhaustive status documentation).
/// </summary>
/// <remarks>
/// <c>POST</c> carries the literal <c>[IdempotencyKeyRequired]</c> guard (Article VI.4) so a
/// retried registration cannot create a duplicate employee; <c>PUT</c> is naturally idempotent
/// and carries the rowversion token for optimistic concurrency instead.
/// </remarks>
[ApiController]
[Route("api/v1/hr/employees")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
public sealed class EmployeesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public EmployeesController(
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
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Rows per page.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [Authorize(Policy = "permission:employee:read")]
    [ProducesResponseType(typeof(PagedResult<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] Guid companyId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidCompany"),
                _errors.Text(HrPayrollErrorCodes.CompanyNotFound),
                HrPayrollErrorCodes.CompanyNotFound);
        }

        var employees = await _sender.SendAsync(new GetEmployeesQuery(companyId, page, pageSize), cancellationToken);
        return Ok(employees);
    }

    /// <summary>
    /// Registers one employee; when <c>salaryStructureId</c> is supplied, the initial structure
    /// assignment is created atomically from the joining date.
    /// </summary>
    /// <remarks>
    /// Requires the <c>Idempotency-Key</c> header (Article VI.4). A missing header is a 400, a
    /// replayed key returns the stored response verbatim, and reusing a key with a different
    /// payload is a 409.
    /// </remarks>
    /// <param name="request">Identity, dates, pay mode/status, bank data and optional structure.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [Authorize(Policy = "permission:employee:write")]
    [IdempotencyKeyRequired]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CompanyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidCompany"),
                _errors.Text(HrPayrollErrorCodes.CompanyNotFound),
                HrPayrollErrorCodes.CompanyNotFound);
        }

        var result = await _sender.SendAsync(
            new CreateEmployeeCommand(
                request.CompanyId,
                request.EmployeeNumber,
                request.FirstName,
                request.LastName,
                request.WorkEmail,
                request.DateOfJoining,
                request.DateOfRelieving,
                request.SalaryMode,
                request.BankName,
                request.BankAccountNumber,
                request.Status,
                request.SalaryStructureId),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return EmployeeProblem(result.Error!);
        }

        return Created($"/api/v1/hr/employees/{result.Value!.Id}", result.Value);
    }

    /// <summary>Updates one employee's staff record. Carries the optimistic <c>rowVersion</c> token (base64).</summary>
    /// <param name="id">Employee id.</param>
    /// <param name="request">Editable fields plus the rowversion token.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "permission:employee:write")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || request.CompanyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidCompany"),
                _errors.Text(HrPayrollErrorCodes.EmployeeNotFound),
                HrPayrollErrorCodes.EmployeeNotFound);
        }

        var result = await _sender.SendAsync(
            new UpdateEmployeeCommand(
                request.CompanyId,
                id,
                request.EmployeeNumber,
                request.FirstName,
                request.LastName,
                request.WorkEmail,
                request.DateOfJoining,
                request.DateOfRelieving,
                request.SalaryMode,
                request.BankName,
                request.BankAccountNumber,
                request.Status,
                request.RowVersion),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return EmployeeProblem(result.Error!);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// RFC 7807 mapping for the employee routes: missing employee/company/structure are 404,
    /// duplicate numbers and concurrency races are 409, every other domain failure is a 400
    /// carrying the stable machine code.
    /// </summary>
    private ObjectResult EmployeeProblem(Error error) =>
        error.Code switch
        {
            HrPayrollErrorCodes.EmployeeNotFound
                or HrPayrollErrorCodes.CompanyNotFound
                or HrPayrollErrorCodes.StructureNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("PayrollResourceNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            HrPayrollErrorCodes.DuplicateEmployeeNumber
                or ConcurrencyErrorCodes.ConcurrencyConflict => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("PayrollConflict"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            _ => Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("PayrollRejected"),
                _errors.Text(error.Code, error.Message),
                error.Code),
        };

    private ObjectResult Problem(int status, string title, string detail, string? code)
    {
        var problem = new ProblemDetails
        {
            Type = status switch
            {
                StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                _ => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            },
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
