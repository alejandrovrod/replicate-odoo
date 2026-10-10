using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AssetHub.Application.Assets.Commands;
using AssetHub.Application.Assets.Queries;
using AssetHub.Infrastructure.Billing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/assets")]
[Authorize]
public class AssetsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AssetsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] Guid? templateId, [FromQuery] string? state, [FromQuery] Guid? ancestorId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _mediator.Send(new SearchAssetsQuery(q, templateId, state, null, ancestorId, false, page, pageSize));
        return Ok(result);
    }

    [HttpPost("search")]
    public async Task<IActionResult> AdvancedSearch([FromBody] AdvancedSearchRequest request)
    {
        request ??= new AdvancedSearchRequest();
        var result = await _mediator.Send(new SearchAssetsQuery(
            request.SearchTerm, 
            request.TemplateId, 
            request.State, 
            request.CatalogFilters, 
            request.AncestorId,
            request.RootOnly,
            request.Page,
            request.PageSize));
        return Ok(result);
    }

    [HttpGet("search-filters")]
    public async Task<IActionResult> GetSearchFilters()
    {
        var result = await _mediator.Send(new GetActiveSearchFiltersQuery());
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetAssetByIdQuery(id));
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("{id}/events")]
    public async Task<IActionResult> GetEvents(Guid id)
    {
        var result = await _mediator.Send(new GetAssetLifecycleEventsQuery(id));
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteAssetCommand(id));
        return NoContent();
    }

    [HttpGet("{id}/costs")]
    public async Task<IActionResult> GetCosts(Guid id, [FromQuery] bool includeSubtree = false)
    {
        var result = await _mediator.Send(new AssetHub.Application.Analytics.Queries.GetAssetCostsQuery { AssetId = id, IncludeSubtree = includeSubtree });
        return Ok(new { Cost = result });
    }

    [HttpGet("{id}/timeline")]
    public async Task<IActionResult> GetTimeline(Guid id)
    {
        var result = await _mediator.Send(new AssetHub.Application.Analytics.Queries.GetAssetTimelineQuery { AssetId = id });
        return Ok(result);
    }

    [HttpGet("{id}/condition-evolution")]
    public async Task<IActionResult> GetConditionEvolution(Guid id)
    {
        var result = await _mediator.Send(new AssetHub.Application.Analytics.Queries.GetAssetConditionEvolutionQuery { AssetId = id });
        return Ok(result);
    }

    [HttpGet("{id}/life-projection")]
    public async Task<IActionResult> GetLifeProjection(Guid id, [FromQuery] decimal endOfLifeThreshold = 20m)
    {
        var result = await _mediator.Send(new AssetHub.Application.Analytics.Queries.GetAssetLifeProjectionQuery { AssetId = id, EndOfLifeThreshold = endOfLifeThreshold });
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAssetRequest request)
    {
        var id = await _mediator.Send(new CreateAssetCommand(
            request.AssetTemplateId,
            request.ParentId,
            request.Code,
            request.Name,
            request.InstalledAt,
            request.CommissionedAt,
            request.ConditionIndex,
            request.PropertiesJson,
            request.GeoJson
        ));
        return Created($"/api/v1/assets/{id}", new { id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateAssetRequest request)
    {
        var success = await _mediator.Send(new UpdateAssetCommand(
            id,
            request.Code,
            request.Name,
            request.InstalledAt,
            request.CommissionedAt,
            request.ConditionIndex,
            request.PropertiesJson,
            request.GeoJson
        ));
        if (!success) return NotFound();
        return Ok();
    }
    
    [HttpPatch("{id}/move")]
    public async Task<IActionResult> Move([FromRoute] Guid id, [FromBody] MoveAssetRequest request)
    {
        var success = await _mediator.Send(new MoveAssetCommand(id, request.NewParentId));
        return Ok();
    }
    
    [HttpPatch("{id}/state")]
    public async Task<IActionResult> ChangeState([FromRoute] Guid id, [FromBody] ChangeStateRequest request)
    {
        var success = await _mediator.Send(new ChangeAssetEnvironmentStateCommand(id, request.ToState, request.Notes, request.TransitionData));
        if (!success) return NotFound();
        return Ok();
    }

    [HttpGet("{id}/attachments")]
    public async Task<IActionResult> GetAttachments(Guid id)
    {
        var result = await _mediator.Send(new GetAssetAttachmentsQuery(id));
        return Ok(new { items = result });
    }

    [HttpPost("{id}/attachments")]
    public async Task<IActionResult> UploadAttachment(Guid id, Microsoft.AspNetCore.Http.IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("File is empty");

        using var stream = file.OpenReadStream();
        var attachmentId = await _mediator.Send(new UploadAssetAttachmentCommand(
            id,
            file.FileName,
            file.ContentType,
            file.Length,
            stream
        ));

        return Ok(new { id = attachmentId });
    }

    [HttpGet("geo")]
    [RequirePlanLimits("geo")]
    public async Task<IActionResult> GetInBoundingBox([FromQuery] double minLon, [FromQuery] double minLat, [FromQuery] double maxLon, [FromQuery] double maxLat)
    {
        var result = await _mediator.Send(new GetAssetsInBoundingBoxQuery(minLon, minLat, maxLon, maxLat));
        return Ok(result);
    }

    [HttpGet("geo/nearby")]
    [RequirePlanLimits("geo")]
    public async Task<IActionResult> GetNearby([FromQuery] double lon, [FromQuery] double lat, [FromQuery] double radius)
    {
        var result = await _mediator.Send(new GetAssetsNearbyQuery(lon, lat, radius));
        return Ok(result);
    }

    [HttpPost("predictions/batch")]
    public async Task<IActionResult> RecordPredictionBatch([FromBody] RecordAssetPredictionBatchCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(new { Processed = result });
    }

    [HttpGet("{id}/health-forecast")]
    public async Task<IActionResult> GetHealthForecast(Guid id)
    {
        var result = await _mediator.Send(new GetAssetHealthForecastQuery(id));
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("{id}/materials")]
    public async Task<IActionResult> GetMaterials(Guid id, [FromQuery] bool? isCritical, [FromQuery] string? search)
    {
        var result = await _mediator.Send(new GetAssetMaterialsQuery(id, isCritical, search));
        return Ok(result);
    }

    [HttpPost("{id}/materials")]
    public async Task<IActionResult> AddMaterial(Guid id, [FromBody] AddMaterialRequest request)
    {
        var materialId = await _mediator.Send(new CreateAssetMaterialCommand(
            id,
            request.CatalogItemId,
            request.Quantity,
            request.UnitOfMeasure,
            request.IsCritical,
            request.Notes
        ));
        return Ok(new { id = materialId });
    }

    [HttpPut("{id}/materials/{materialId}")]
    public async Task<IActionResult> UpdateMaterial(Guid id, Guid materialId, [FromBody] UpdateMaterialRequest request)
    {
        await _mediator.Send(new UpdateAssetMaterialCommand(
            materialId,
            request.Quantity,
            request.UnitOfMeasure,
            request.IsCritical,
            request.Notes
        ));
        return Ok();
    }

    [HttpDelete("{id}/materials/{materialId}")]
    public async Task<IActionResult> DeleteMaterial(Guid id, Guid materialId)
    {
        await _mediator.Send(new DeleteAssetMaterialCommand(materialId));
        return NoContent();
    }
}

public class AdvancedSearchRequest
{
    public string? SearchTerm { get; set; }
    public Guid? TemplateId { get; set; }
    public string? State { get; set; }
    public Guid? AncestorId { get; set; }
    public Dictionary<string, Guid>? CatalogFilters { get; set; }
    public bool? RootOnly { get; set; }
    public Guid? AssetId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class CreateAssetRequest
{
    public Guid AssetTemplateId { get; set; }
    public Guid? ParentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime? InstalledAt { get; set; }
    public DateTime? CommissionedAt { get; set; }
    public decimal? ConditionIndex { get; set; }
    public string PropertiesJson { get; set; } = "{}";
    public string? GeoJson { get; set; }
}

public class UpdateAssetRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime? InstalledAt { get; set; }
    public DateTime? CommissionedAt { get; set; }
    public decimal? ConditionIndex { get; set; }
    public string PropertiesJson { get; set; } = "{}";
    public string? GeoJson { get; set; }
}

public class MoveAssetRequest
{
    public Guid? NewParentId { get; set; }
}

public class ChangeStateRequest
{
    public string ToState { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public Dictionary<string, System.Text.Json.JsonElement>? TransitionData { get; set; }
}

public class AddMaterialRequest
{
    public Guid CatalogItemId { get; set; }
    public decimal Quantity { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public bool IsCritical { get; set; }
    public string? Notes { get; set; }
}

public class UpdateMaterialRequest
{
    public decimal Quantity { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public bool IsCritical { get; set; }
    public string? Notes { get; set; }
}

