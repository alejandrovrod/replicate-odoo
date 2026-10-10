using Erp.Application.Common;
using Erp.Application.Features.SystemBase.Companies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Erp.Api.Controllers.V1;

[ApiController]
[Route("api/v1/companies")]
public class CompaniesController : ControllerBase
{
    private readonly ISender _sender;

    public CompaniesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "permission:company:read")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _sender.SendAsync(new GetCompanyQuery(id));
        return Ok(result);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "permission:company:write")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCompanyCommand command)
    {
        if (id != command.Id) return BadRequest();
        var result = await _sender.SendAsync(command);
        return Ok(result);
    }
}
