using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Services;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Presets.Commands;

/// <summary>Applies one industry preset to a company (idempotent re-apply skips existing).</summary>
public sealed record ApplyPresetCommand(string PresetKey, Guid CompanyId)
    : ICommand<Result<PresetApplyDto>>;

/// <summary>What one preset application created vs skipped (overlay admin report).</summary>
public sealed record PresetApplyDto(
    string PresetKey,
    int CustomFieldsCreated,
    int CustomFieldsSkipped,
    int WorkflowsCreated,
    int WorkflowsSkipped,
    int TaxTemplatesCreated,
    int TaxTemplatesSkipped);

/// <summary>One bundled preset with display metadata (preset browser source).</summary>
public sealed record VerticalPresetDto(
    string Key,
    string Name,
    string Description,
    IReadOnlyList<string> Languages);

public sealed class ApplyPresetCommandHandler : ICommandHandler<ApplyPresetCommand, Result<PresetApplyDto>>
{
    private readonly IVerticalPresetService _presets;

    public ApplyPresetCommandHandler(IVerticalPresetService presets)
    {
        _presets = presets;
    }

    public async Task<Result<PresetApplyDto>> HandleAsync(
        ApplyPresetCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _presets.ApplyAsync(command.PresetKey, command.CompanyId, cancellationToken);
            return Result<PresetApplyDto>.Success(new PresetApplyDto(
                result.PresetKey,
                result.CustomFieldsCreated,
                result.CustomFieldsSkipped,
                result.WorkflowsCreated,
                result.WorkflowsSkipped,
                result.TaxTemplatesCreated,
                result.TaxTemplatesSkipped));
        }
        catch (WorkflowValidationException ex)
        {
            return Result<PresetApplyDto>.Failure(ex.Code, ex.Message);
        }
    }
}
