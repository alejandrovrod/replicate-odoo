using System.Threading.Tasks;
using AssetHub.Application.Tenants.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/media")]
public class MediaController : ControllerBase
{
    private readonly IMediator _mediator;

    public MediaController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("upload")]
    [Authorize(Policy = "permission:tenant:write")] // Permisos para subir media del tenant
    public async Task<IActionResult> UploadMedia(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("File is required.");
        }

        using var stream = file.OpenReadStream();
        var command = new UploadTenantMediaCommand(
            file.FileName,
            file.ContentType,
            file.Length,
            stream
        );

        var url = await _mediator.Send(command);

        return Ok(new { url });
    }
}
