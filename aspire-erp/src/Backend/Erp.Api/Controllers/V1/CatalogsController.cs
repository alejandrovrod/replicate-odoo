using Erp.Application.Common;
using Erp.Application.Features.Catalogs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Erp.Api.Controllers.V1;

[ApiController]
[Route("v1/catalogs")]
public class CatalogsController : ControllerBase
{
    private readonly ISender _sender;

    public CatalogsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("{code}")]
    public async Task<ActionResult<List<CatalogItemDto>>> GetByCode(string code, [FromQuery] string lang = "en")
    {
        var result = await _sender.SendAsync(new GetCatalogByCodeQuery(code, lang));
        return Ok(result);
    }
}
