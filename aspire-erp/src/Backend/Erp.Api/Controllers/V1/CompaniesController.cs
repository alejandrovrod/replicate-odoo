using Erp.Application.Common;
using Erp.Application.Features.SystemBase.Companies;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.V1;

[ApiController]
[Route("v1/companies")]
public class CompaniesController : ControllerBase
{
    private readonly ISender _sender;

    public CompaniesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCompanyCommand command)
    {
        if (id != command.Id) return BadRequest();
        var result = await _sender.SendAsync(command);
        return Ok(result);
    }
}
