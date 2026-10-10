using System.Threading.Tasks;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.HrPayroll.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Erp.Api.Controllers.V1;

[ApiController]
[Route("api/v1/hr")]
public class HrController : ControllerBase
{
    private readonly ISender _sender;

    public HrController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("attendance")]
    [Authorize(Policy = "permission:attendance:write")]
    [ProducesResponseType(typeof(AttendanceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAttendance([FromBody] CreateAttendanceCommand command)
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

        return CreatedAtAction(nameof(CreateAttendance), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPost("leave-applications")]
    [Authorize(Policy = "permission:leave_application:write")]
    [ProducesResponseType(typeof(LeaveApplicationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateLeaveApplication([FromBody] CreateLeaveApplicationCommand command)
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

        return CreatedAtAction(nameof(CreateLeaveApplication), new { id = result.Value!.Id }, result.Value);
    }
}
