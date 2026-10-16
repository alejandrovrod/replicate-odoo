using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.Features.Presets.Commands;
using Erp.Application.Features.Presets.Queries;
using Erp.Application.Services;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Industry preset endpoints (Fase 5): browse bundled verticals, apply one to a company
/// (custom fields + workflows + tax templates, idempotent), and read the active preset
/// with its terminology map (overlay endpoint source - wire contract stays canonical).
/// </summary>
[ApiController]
[Route("api/v1/presets")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class VerticalPresetsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public VerticalPresetsController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns every bundled industry preset (preset browser source).</summary>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [Authorize(Policy = "permission:preset:read")]
    [ProducesResponseType(typeof(IReadOnlyList<VerticalPresetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var presets = await _sender.SendAsync(new GetPresetsQuery(), cancellationToken);
        return Ok(presets);
    }

    /// <summary>Active preset + terminology map (overlay endpoint source).</summary>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("active")]
    [Authorize(Policy = "permission:preset:read")]
    [ProducesResponseType(typeof(ActivePresetDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var active = await _sender.SendAsync(new GetActivePresetQuery(), cancellationToken);
        return Ok(active);
    }

    /// <summary>Applies one preset to a company (idempotent re-apply skips existing rows).</summary>
    /// <param name="command">Preset key + target company.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("apply")]
    [Authorize(Policy = "permission:preset:write")]
    [ProducesResponseType(typeof(PresetApplyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Apply(
        [FromBody] ApplyPresetCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                WorkflowErrorCodes.PresetNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("PresetNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("PresetRejected"),
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
