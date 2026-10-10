using System;
using System.Linq;
using System.Threading.Tasks;
using AssetHub.Application.CommunicationTemplates.Commands;
using AssetHub.Application.CommunicationTemplates.Queries;
using AssetHub.Domain.CommunicationTemplates;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/templates")]
[Authorize]
public class TemplatesController : ControllerBase
{
    private readonly IMediator _mediator;

    public TemplatesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetTemplates([FromQuery] GetCommunicationTemplatesQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTemplateById(Guid id)
    {
        var result = await _mediator.Send(new GetCommunicationTemplateByIdQuery { TemplateId = id });
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "permission:communication-templates:manage")]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateCommunicationTemplateCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetTemplateById), new { id }, new { id });
    }

    [HttpPost("{id}/versions")]
    [Authorize(Policy = "permission:communication-templates:manage")]
    public async Task<IActionResult> AddVersion(Guid id, [FromBody] AddCommunicationTemplateVersionCommand command)
    {
        command.TemplateId = id;
        var versionId = await _mediator.Send(command);
        return Ok(new { id = versionId });
    }

    [HttpPut("{id}/versions/{versionId}")]
    [Authorize(Policy = "permission:communication-templates:manage")]
    public async Task<IActionResult> UpdateVersion(Guid id, Guid versionId, [FromBody] UpdateCommunicationTemplateVersionCommand command)
    {
        command.TemplateId = id;
        command.VersionId = versionId;
        var updatedVersionId = await _mediator.Send(command);
        return Ok(new { id = updatedVersionId });
    }

    [HttpPut("{id}/versions/{versionId}/activate")]
    [Authorize(Policy = "permission:communication-templates:manage")]
    public async Task<IActionResult> ActivateVersion(Guid id, Guid versionId)
    {
        await _mediator.Send(new ActivateCommunicationTemplateVersionCommand { TemplateId = id, VersionId = versionId });
        return NoContent();
    }

    [HttpPost("{id}/render")]
    public async Task<IActionResult> RenderTemplate(Guid id, [FromBody] RenderTemplateRequest request)
    {
        var result = await _mediator.Send(new RenderCommunicationTemplateQuery
        {
            TemplateId = id,
            Locale = request.Locale,
            SampleVariables = request.Variables
        });
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("{id}/test-email")]
    [Authorize(Policy = "permission:communication-templates:manage")]
    public async Task<IActionResult> SendTestEmail(Guid id, [FromBody] SendTestEmailRequest request)
    {
        await _mediator.Send(new SendTestEmailCommand
        {
            TemplateId = id,
            To = request.To,
            Locale = request.Locale,
            Translations = request.Translations?
                .Select(t => new SendTestEmailCommand.TestTranslationInput
                {
                    Locale = t.Locale,
                    Subject = t.Subject,
                    Content = t.Content
                })
                .ToList()
        });
        return Ok(new { sent = true });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "permission:communication-templates:manage")]
    public async Task<IActionResult> DeleteTemplate(Guid id)
    {
        await _mediator.Send(new DeleteCommunicationTemplateCommand { TemplateId = id });
        return NoContent();
    }
}

public class RenderTemplateRequest
{
    public string Locale { get; set; } = "es";
    public System.Collections.Generic.Dictionary<string, string>? Variables { get; set; }
}

public class SendTestEmailRequest
{
    public string To { get; set; } = string.Empty;
    public string Locale { get; set; } = "es";
    public System.Collections.Generic.List<TestTranslationDto>? Translations { get; set; }

    public class TestTranslationDto
    {
        public string Locale { get; set; } = string.Empty;
        public string? Subject { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}
