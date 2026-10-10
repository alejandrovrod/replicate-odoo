using System.Threading.Tasks;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Projects.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly ISender _sender;

    public ProjectsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [Authorize(Policy = "permission:project:write")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateProjectCommand command)
    {
        var result = await _sender.SendAsync(command);
        
        if (!result.IsSuccess)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation Failed",
                Detail = result.Error?.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return CreatedAtAction(nameof(Create), new { id = result.Value!.Id }, result.Value);
    }
}
