using System;
using System.Threading.Tasks;
using AssetHub.Application.Finance.Commands;
using AssetHub.Application.Finance.Dtos;
using AssetHub.Application.Finance.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/assets")]
[Authorize]
public class AssetFinanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public AssetFinanceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    #region Finance Profile

    [HttpGet("{id}/finance-profile")]
    [Authorize(Policy = "permission:assets.finance:read")]
    public async Task<IActionResult> GetFinanceProfile(Guid id)
    {
        var result = await _mediator.Send(new GetFinanceProfileQuery(id));
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPut("{id}/finance-profile")]
    [Authorize(Policy = "permission:assets.finance:write")]
    public async Task<IActionResult> UpsertFinanceProfile(Guid id, [FromBody] UpsertFinanceBookRequest request)
    {
        var result = await _mediator.Send(new UpsertFinanceBookCommand(id, request));
        return Ok(result);
    }

    [HttpDelete("{id}/finance-profile")]
    [Authorize(Policy = "permission:assets.finance:write")]
    public async Task<IActionResult> DeleteFinanceProfile(Guid id)
    {
        var success = await _mediator.Send(new DeleteFinanceBookCommand(id));
        if (!success) return NotFound();
        return NoContent();
    }

    #endregion

    #region Depreciation Schedules

    [HttpGet("{id}/depreciation-schedules")]
    [Authorize(Policy = "permission:assets.finance:read")]
    public async Task<IActionResult> GetDepreciationSchedules(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] bool? onlyPending = null)
    {
        var result = await _mediator.Send(new GetDepreciationSchedulesQuery(id, page, pageSize, onlyPending));
        return Ok(result);
    }

    [HttpPost("{id}/depreciation-schedules/generate")]
    [Authorize(Policy = "permission:assets.finance:write")]
    public async Task<IActionResult> GenerateDepreciationSchedule(Guid id, [FromBody] GenerateScheduleRequest request)
    {
        var result = await _mediator.Send(new GenerateDepreciationScheduleCommand(id, request));
        return Ok(result);
    }

    [HttpPost("{id}/depreciation-schedules/recalculate")]
    [Authorize(Policy = "permission:assets.finance:write")]
    public async Task<IActionResult> RecalculateDepreciationSchedule(Guid id, [FromBody] RecalculateScheduleRequest request)
    {
        var result = await _mediator.Send(new RecalculateDepreciationScheduleCommand(id, request));
        return Ok(result);
    }

    #endregion

    #region Depreciation Entries (Posting)

    [HttpGet("{id}/depreciation-entries")]
    [Authorize(Policy = "permission:assets.finance:read")]
    public async Task<IActionResult> GetDepreciationEntries(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _mediator.Send(new GetDepreciationEntriesQuery(id, page, pageSize));
        return Ok(result);
    }

    [HttpPost("{id}/depreciation-entries/post")]
    [Authorize(Policy = "permission:assets.depreciation:post")]
    public async Task<IActionResult> PostDepreciationEntries(Guid id, [FromBody] PostDepreciationRequest request)
    {
        var result = await _mediator.Send(new PostDepreciationEntriesCommand(id, request));
        return Ok(result);
    }

    #endregion

    #region Value Adjustments

    [HttpGet("{id}/value-adjustments")]
    [Authorize(Policy = "permission:assets.finance:read")]
    public async Task<IActionResult> GetValueAdjustments(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _mediator.Send(new GetValueAdjustmentsQuery(id, page, pageSize));
        return Ok(result);
    }

    [HttpPost("{id}/value-adjustments")]
    [Authorize(Policy = "permission:assets.finance:write")]
    public async Task<IActionResult> CreateValueAdjustment(Guid id, [FromBody] CreateValueAdjustmentRequest request)
    {
        var result = await _mediator.Send(new CreateValueAdjustmentCommand(id, request));
        return Created($"/api/v1/assets/{id}/value-adjustments/{result.Id}", result);
    }

    #endregion

    #region Disposal

    [HttpGet("{id}/disposal")]
    [Authorize(Policy = "permission:assets.disposal:read")]
    public async Task<IActionResult> GetDisposal(Guid id)
    {
        var result = await _mediator.Send(new GetDisposalQuery(id));
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("{id}/disposal")]
    [Authorize(Policy = "permission:assets.disposal:write")]
    public async Task<IActionResult> CreateDisposal(Guid id, [FromBody] CreateDisposalRequest request)
    {
        var result = await _mediator.Send(new CreateDisposalCommand(id, request));
        return Created($"/api/v1/assets/{id}/disposal", result);
    }

    #endregion

    #region Custody Transfers

    [HttpGet("{id}/custody-transfers")]
    [Authorize(Policy = "permission:assets.custody:read")]
    public async Task<IActionResult> GetCustodyTransfers(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _mediator.Send(new GetCustodyTransfersQuery(id, page, pageSize));
        return Ok(result);
    }

    [HttpPost("{id}/custody-transfers")]
    [Authorize(Policy = "permission:assets.custody:write")]
    public async Task<IActionResult> CreateCustodyTransfer(Guid id, [FromBody] CreateCustodyTransferRequest request)
    {
        var result = await _mediator.Send(new CreateCustodyTransferCommand(id, request));
        return Created($"/api/v1/assets/{id}/custody-transfers/{result.Id}", result);
    }

    #endregion

    #region Finance Summary

    [HttpGet("{id}/finance-summary")]
    [Authorize(Policy = "permission:assets.finance:read")]
    public async Task<IActionResult> GetFinanceSummary(Guid id)
    {
        var result = await _mediator.Send(new GetFinanceSummaryQuery(id));
        if (result == null) return NotFound();
        return Ok(result);
    }

    #endregion
}