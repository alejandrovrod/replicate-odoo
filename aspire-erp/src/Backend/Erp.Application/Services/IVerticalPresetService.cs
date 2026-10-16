using System.Text.Json;
using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Services;

/// <summary>One preset listed by the catalog (key + display names + terminology coverage).</summary>
public sealed record VerticalPresetSummary(
    string Key,
    string Name,
    string Description,
    IReadOnlyList<string> Languages);

/// <summary>Active-preset read for the terminology overlay endpoint.</summary>
public sealed record ActivePresetDto(
    string? PresetKey,
    string? Name,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Terminology);

/// <summary>What one preset application created vs skipped (idempotent re-apply).</summary>
public sealed record PresetApplyResult(
    string PresetKey,
    int CustomFieldsCreated,
    int CustomFieldsSkipped,
    int WorkflowsCreated,
    int WorkflowsSkipped,
    int TaxTemplatesCreated,
    int TaxTemplatesSkipped);

/// <summary>
/// Industry vertical presets (workflow Fase 5, gap D3): embedded JSON seed packs applied
/// atomically to a tenant - custom fields, workflows and tax templates become ordinary
/// catalog rows (individually editable afterwards); terminology stays served live from
/// the JSON via the active-preset record. Re-applying is idempotent (existing rows are
/// skipped, never duplicated).
/// </summary>
public interface IVerticalPresetService
{
    /// <summary>All bundled presets with their display metadata.</summary>
    IReadOnlyList<VerticalPresetSummary> List();

    /// <summary>Loads one bundled preset document (throws PresetNotFound when unknown).</summary>
    PresetDocument Load(string presetKey);

    /// <summary>
    /// Applies one preset to a company of the current tenant, atomically. Custom fields and
    /// workflows are tenant-wide; tax templates land on the given company (their accounts
    /// resolve by code, unresolvable lines are skipped). Returns the report.
    /// </summary>
    Task<PresetApplyResult> ApplyAsync(string presetKey, Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>Active preset + its terminology map (overlay endpoint source).</summary>
    Task<ActivePresetDto> GetActiveAsync(CancellationToken cancellationToken = default);
}

/// <summary>Deserialized preset document (embedded JSON shape, all sections optional).</summary>
public sealed class PresetDocument
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, Dictionary<string, Dictionary<string, string>>> Terminology { get; set; } = new();
    public List<PresetCustomField> CustomFields { get; set; } = new();
    public List<PresetWorkflow> Workflows { get; set; } = new();
    public List<PresetTaxTemplate> TaxTemplates { get; set; } = new();
}

public sealed class PresetCustomField
{
    public string Entity { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public bool Required { get; set; }
    public string? Default { get; set; }
    public string? Schema { get; set; }
}

public sealed class PresetWorkflowState
{
    public string Name { get; set; } = string.Empty;
    public bool Initial { get; set; }
    public bool CanSubmit { get; set; }
}

public sealed class PresetWorkflowTransition
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public sealed class PresetWorkflow
{
    public string DocumentType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<PresetWorkflowState> States { get; set; } = new();
    public List<PresetWorkflowTransition> Transitions { get; set; } = new();
}

public sealed class PresetTaxLine
{
    public string AccountCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public string ChargeType { get; set; } = "Add";
    public string Basis { get; set; } = "OnNetTotal";
    public decimal Amount { get; set; }
}

public sealed class PresetTaxTemplate
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "Sales";
    public List<PresetTaxLine> Lines { get; set; } = new();
}
