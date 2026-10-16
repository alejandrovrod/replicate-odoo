using Erp.Application.Common;
using Erp.Application.Features.Presets.Commands;
using Erp.Application.Services;

namespace Erp.Application.Features.Presets.Queries;

/// <summary>Every bundled industry preset (preset browser source).</summary>
public sealed record GetPresetsQuery : IQuery<IReadOnlyList<VerticalPresetDto>>;

/// <summary>Active preset + terminology map (overlay endpoint source).</summary>
public sealed record GetActivePresetQuery : IQuery<ActivePresetDto>;

public sealed class GetPresetsQueryHandler : IQueryHandler<GetPresetsQuery, IReadOnlyList<VerticalPresetDto>>
{
    private readonly IVerticalPresetService _presets;

    public GetPresetsQueryHandler(IVerticalPresetService presets)
    {
        _presets = presets;
    }

    public Task<IReadOnlyList<VerticalPresetDto>> HandleAsync(
        GetPresetsQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<VerticalPresetDto>>(
            _presets.List().Select(p => new VerticalPresetDto(p.Key, p.Name, p.Description, p.Languages)).ToList());
}

public sealed class GetActivePresetQueryHandler : IQueryHandler<GetActivePresetQuery, ActivePresetDto>
{
    private readonly IVerticalPresetService _presets;

    public GetActivePresetQueryHandler(IVerticalPresetService presets)
    {
        _presets = presets;
    }

    public async Task<ActivePresetDto> HandleAsync(
        GetActivePresetQuery query, CancellationToken cancellationToken = default)
    {
        var active = await _presets.GetActiveAsync(cancellationToken);
        return new ActivePresetDto(active.PresetKey, active.Name, active.Terminology);
    }
}
