using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Extensibility.Commands;
using Erp.Application.Features.Extensibility.Queries;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Custom field catalog endpoints (extensibility-engine Fase 1): tenant-scoped definitions
/// that declare which dynamic attributes each root entity accepts inside
/// <c>CustomData</c>. Route resolves to <c>/api/v1/customfielddefinitions</c>. No
/// <c>[IdempotencyKeyRequired]</c>: catalog rows are config, never ledger postings (same
/// reasoning as CustomersController).
/// </summary>
/// <remarks>
/// <para><b>Status mapping</b>: <c>duplicate_custom_field</c> -&gt; 409;
/// <c>custom_field_definition_not_found</c> -&gt; 404; everything else
/// (<c>custom_field_validation_failed</c>, <c>invalid_field_type</c>) describes a bad
/// REQUEST -&gt; 400.</para>
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class CustomFieldDefinitionsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public CustomFieldDefinitionsController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns the active definitions for one root entity (powers validation + UI rendering).</summary>
    /// <param name="entityName">Root entity name, e.g. "Customer".</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [Authorize(Policy = "permission:custom_field:read")]
    [ProducesResponseType(typeof(IReadOnlyList<CustomFieldDefinitionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] string entityName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entityName))
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidCustomField"),
                _errors.Text(ExtensibilityErrorCodes.CustomFieldValidationFailed),
                ExtensibilityErrorCodes.CustomFieldValidationFailed);
        }

        var definitions = await _sender.SendAsync(
            new GetCustomFieldDefinitionsQuery(entityName.Trim()), cancellationToken);
        return Ok(definitions);
    }

    /// <summary>Registers one custom field for a root entity.</summary>
    /// <param name="command">Entity, field name, type, requirement and constraints.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [Authorize(Policy = "permission:custom_field:write")]
    [ProducesResponseType(typeof(CustomFieldDefinitionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCustomFieldDefinitionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                ExtensibilityErrorCodes.DuplicateCustomField => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("CustomFieldConflict"),
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("CustomFieldRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        var dto = result.Value!;
        return CreatedAtAction(nameof(Get), new { entityName = dto.EntityName }, dto);
    }

    /// <summary>Updates requirement, defaults, schema or active flag of one definition.</summary>
    /// <param name="id">Definition ID.</param>
    /// <param name="command">Updated data including the same ID.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "permission:custom_field:write")]
    [ProducesResponseType(typeof(CustomFieldDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCustomFieldDefinitionCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidCustomField"),
                _errors.Text(ExtensibilityErrorCodes.CustomFieldDefinitionNotFound),
                ExtensibilityErrorCodes.CustomFieldDefinitionNotFound);
        }

        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                ExtensibilityErrorCodes.CustomFieldDefinitionNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("CustomFieldNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("CustomFieldRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        return Ok(result.Value!);
    }

    /// <summary>Deletes one catalog row (config, never ledger data).</summary>
    /// <param name="id">Definition ID.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "permission:custom_field:write")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(new DeleteCustomFieldDefinitionCommand(id), cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return Problem(
                StatusCodes.Status404NotFound,
                _common.Text("CustomFieldNotFound"),
                _errors.Text(error.Code, error.Message),
                error.Code);
        }

        return NoContent();
    }

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
