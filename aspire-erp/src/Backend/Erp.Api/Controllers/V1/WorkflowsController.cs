using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Workflow.Commands;
using Erp.Application.Features.Workflow.Queries;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Operational workflow endpoints (Fase 5): definition catalog plus per-document status
/// and transitions. Route resolves to <c>/api/v1/workflows</c>. No
/// <c>[IdempotencyKeyRequired]</c>: transitions only move free-text operational state and
/// write no ledger rows.
/// </summary>
/// <remarks>
/// <para><b>Status mapping</b>: <c>duplicate_workflow</c> -&gt; 409;
/// <c>workflow_not_found</c> -&gt; 404; <c>workflow_role_unauthorized</c> -&gt; 403;
/// everything else describes a bad REQUEST -&gt; 400.</para>
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class WorkflowsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public WorkflowsController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns every workflow definition of the tenant (admin list).</summary>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("definitions")]
    [Authorize(Policy = "permission:workflow:read")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkflowDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDefinitions(CancellationToken cancellationToken)
    {
        var definitions = await _sender.SendAsync(new GetWorkflowDefinitionsQuery(), cancellationToken);
        return Ok(definitions);
    }

    /// <summary>Registers one operational workflow (states + transitions validated as a whole).</summary>
    /// <param name="command">Document type, name, states and transitions.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("definitions")]
    [Authorize(Policy = "permission:workflow:write")]
    [ProducesResponseType(typeof(WorkflowDefinitionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateDefinition(
        [FromBody] CreateWorkflowDefinitionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                WorkflowErrorCodes.DuplicateWorkflow => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("WorkflowConflict"),
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("WorkflowRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        var dto = result.Value!;
        return CreatedAtAction(nameof(GetDefinitionById), new { id = dto.Id }, dto);
    }

    /// <summary>Returns one workflow definition.</summary>
    /// <param name="id">Definition ID.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("definitions/{id:guid}")]
    [Authorize(Policy = "permission:workflow:read")]
    [ProducesResponseType(typeof(WorkflowDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDefinitionById(Guid id, CancellationToken cancellationToken)
    {
        var definition = await _sender.SendAsync(new GetWorkflowDefinitionByIdQuery(id), cancellationToken);

        return definition is null
            ? Problem(
                StatusCodes.Status404NotFound,
                _common.Text("WorkflowNotFound"),
                _errors.Text(WorkflowErrorCodes.WorkflowNotFound),
                WorkflowErrorCodes.WorkflowNotFound)
            : Ok(definition);
    }

    /// <summary>Replaces one workflow (document type immutable; states/transitions wholesale).</summary>
    /// <param name="id">Definition ID.</param>
    /// <param name="command">Updated data including the same ID.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPut("definitions/{id:guid}")]
    [Authorize(Policy = "permission:workflow:write")]
    [ProducesResponseType(typeof(WorkflowDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateDefinition(
        Guid id,
        [FromBody] UpdateWorkflowDefinitionCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidWorkflow"),
                _errors.Text(WorkflowErrorCodes.WorkflowNotFound),
                WorkflowErrorCodes.WorkflowNotFound);
        }

        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                WorkflowErrorCodes.WorkflowNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("WorkflowNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                WorkflowErrorCodes.DuplicateWorkflow => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("WorkflowConflict"),
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("WorkflowRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        return Ok(result.Value!);
    }

    /// <summary>Deletes one workflow (documents keep their free-text state, unguarded).</summary>
    /// <param name="id">Definition ID.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpDelete("definitions/{id:guid}")]
    [Authorize(Policy = "permission:workflow:write")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDefinition(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(new DeleteWorkflowDefinitionCommand(id), cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return Problem(
                StatusCodes.Status404NotFound,
                _common.Text("WorkflowNotFound"),
                _errors.Text(error.Code, error.Message),
                error.Code);
        }

        return NoContent();
    }

    /// <summary>Current operational state plus executable moves (ActionBar source).</summary>
    /// <param name="documentType">Document type, e.g. "SalesOrder".</param>
    /// <param name="companyId">Company owning the document.</param>
    /// <param name="documentId">Document ID.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("status")]
    [Authorize(Policy = "permission:workflow:read")]
    [ProducesResponseType(typeof(WorkflowStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(
        [FromQuery] string documentType,
        [FromQuery] Guid companyId,
        [FromQuery] Guid documentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(documentType) || companyId == Guid.Empty || documentId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidWorkflow"),
                _errors.Text(WorkflowErrorCodes.WorkflowNotFound),
                WorkflowErrorCodes.WorkflowNotFound);
        }

        var status = await _sender.SendAsync(
            new GetWorkflowStatusQuery(documentType, companyId, documentId), cancellationToken);

        return status is null
            ? Problem(
                StatusCodes.Status404NotFound,
                _common.Text("WorkflowNotFound"),
                _errors.Text(WorkflowErrorCodes.WorkflowNotFound),
                WorkflowErrorCodes.WorkflowNotFound)
            : Ok(status);
    }

    /// <summary>Executes one operational move (role-checked).</summary>
    /// <param name="command">Document type, company, document and target state.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("transition")]
    [Authorize(Policy = "permission:workflow:write")]
    [ProducesResponseType(typeof(WorkflowStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Transition(
        [FromBody] ApplyWorkflowTransitionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                WorkflowErrorCodes.WorkflowNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("WorkflowNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                WorkflowErrorCodes.WorkflowRoleUnauthorized => Problem(
                    StatusCodes.Status403Forbidden,
                    _common.Text("WorkflowForbidden"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("WorkflowRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        return Ok(result.Value!);
    }

    private ObjectResult Problem(int status, string title, string detail, string? code)
    {
        var problem = new ProblemDetails
        {
            Type = status switch
            {
                StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
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
