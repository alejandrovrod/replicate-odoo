using System.Threading.Tasks;
using AssetHub.Application.Catalogs.Commands;
using AssetHub.Application.Catalogs.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/catalogs")]
[Authorize]
public class CatalogsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CatalogsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetCatalogs()
    {
        var result = await _mediator.Send(new GetCatalogsQuery());
        return Ok(new { items = result });
    }

    [HttpPost]
    [Authorize(Policy = "permission:catalogs:create")] // En un entorno real se validaría policies / permissions
    public async Task<IActionResult> CreateCatalog([FromBody] CreateCatalogRequest request)
    {
        var id = await _mediator.Send(new CreateCatalogCommand(request.Code, request.Label, request.TargetModules));
        return Ok(new { id });
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "permission:catalogs:update")]
    public async Task<IActionResult> UpdateCatalog([FromRoute] System.Guid id, [FromBody] UpdateCatalogRequest request)
    {
        var success = await _mediator.Send(new UpdateCatalogCommand(id, request.Label, request.TargetModules));
        if (!success) return NotFound();
        return Ok();
    }
}

public class CreateCatalogRequest
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public System.Collections.Generic.List<string>? TargetModules { get; set; }
}

public class UpdateCatalogRequest
{
    public string Label { get; set; } = string.Empty;
    public System.Collections.Generic.List<string>? TargetModules { get; set; }
}
