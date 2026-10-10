using System.Collections.Generic;
using System.Threading.Tasks;
using AssetHub.Application.Catalogs.Commands;
using AssetHub.Application.Catalogs.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/catalogs/{catalogCode}/items")]
[Authorize]
public class CatalogItemsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CatalogItemsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetItems([FromRoute] string catalogCode, [FromQuery] string locale = "es", [FromQuery] string? search = null)
    {
        var result = await _mediator.Send(new GetCatalogItemsQuery(catalogCode, locale, search));
        return Ok(new { items = result });
    }

    [HttpPost]
    [Authorize(Policy = "permission:catalog-items:create")]
    public async Task<IActionResult> CreateItem([FromRoute] string catalogCode, [FromBody] CreateCatalogItemRequest request)
    {
        var id = await _mediator.Send(new CreateCatalogItemCommand(
            catalogCode, 
            request.Code, 
            request.DefaultLabel, 
            request.Order, 
            request.Translations
        ));
        return Ok(new { id });
    }

    [HttpPut("{itemCode}")]
    [Authorize(Policy = "permission:catalog-items:update")]
    public async Task<IActionResult> UpdateItem([FromRoute] string catalogCode, [FromRoute] string itemCode, [FromBody] UpdateCatalogItemRequest request)
    {
        var success = await _mediator.Send(new UpdateCatalogItemCommand(
            catalogCode, 
            itemCode, 
            request.NewCode,
            request.DefaultLabel, 
            request.Order, 
            request.Translations
        ));
        
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpDelete("{itemCode}")]
    [Authorize(Policy = "permission:catalog-items:delete")]
    public async Task<IActionResult> DeleteItem([FromRoute] string catalogCode, [FromRoute] string itemCode)
    {
        var success = await _mediator.Send(new DeleteCatalogItemCommand(catalogCode, itemCode));
        if (!success) return NotFound();
        return NoContent();
    }
}

public class CreateCatalogItemRequest
{
    public string Code { get; set; } = string.Empty;
    public string DefaultLabel { get; set; } = string.Empty;
    public int Order { get; set; }
    public Dictionary<string, string> Translations { get; set; } = new();
}

public class UpdateCatalogItemRequest
{
    public string? NewCode { get; set; }
    public string DefaultLabel { get; set; } = string.Empty;
    public int Order { get; set; }
    public Dictionary<string, string> Translations { get; set; } = new();
}
