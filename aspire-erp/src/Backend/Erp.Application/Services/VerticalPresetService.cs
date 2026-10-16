using System.Reflection;
using System.Text.Json;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Services;

/// <inheritdoc cref="IVerticalPresetService"/>
public sealed class VerticalPresetService : IVerticalPresetService
{
    private static readonly string[] PresetKeys = ["healthcare", "automotive", "construction", "distribution"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ICustomFieldDefinitionRepository _customFields;
    private readonly IWorkflowRepository _workflows;
    private readonly ITaxTemplateRepository _taxTemplates;
    private readonly IAccountRepository _accounts;
    private readonly ITenantPresetRepository _tenantPresets;

    public VerticalPresetService(
        ICustomFieldDefinitionRepository customFields,
        IWorkflowRepository workflows,
        ITaxTemplateRepository taxTemplates,
        IAccountRepository accounts,
        ITenantPresetRepository tenantPresets)
    {
        _customFields = customFields;
        _workflows = workflows;
        _taxTemplates = taxTemplates;
        _accounts = accounts;
        _tenantPresets = tenantPresets;
    }

    public IReadOnlyList<VerticalPresetSummary> List() =>
        PresetKeys
            .Select(key =>
            {
                var document = Load(key);
                return new VerticalPresetSummary(
                    document.Key,
                    document.Name,
                    document.Description,
                    document.Terminology.Keys.OrderBy(l => l).ToList());
            })
            .ToList();

    public PresetDocument Load(string presetKey)
    {
        var key = (presetKey ?? string.Empty).Trim().ToLowerInvariant();
        if (!PresetKeys.Contains(key))
        {
            throw new WorkflowValidationException(
                WorkflowErrorCodes.PresetNotFound,
                $"Unknown industry preset '{presetKey}'.");
        }

        var resourceName = $"Erp.Application.Presets.{key}.json";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new WorkflowValidationException(
                WorkflowErrorCodes.PresetNotFound,
                $"Industry preset '{key}' is not bundled.");
        using var reader = new StreamReader(stream);
        var document = JsonSerializer.Deserialize<PresetDocument>(reader.ReadToEnd(), JsonOptions)
            ?? throw new WorkflowValidationException(
                WorkflowErrorCodes.PresetNotFound,
                $"Industry preset '{key}' is empty.");

        document.Key = key;
        return document;
    }

    public async Task<PresetApplyResult> ApplyAsync(
        string presetKey, Guid companyId, CancellationToken cancellationToken = default)
    {
        var document = Load(presetKey);

        if (companyId == Guid.Empty)
        {
            throw new WorkflowValidationException(
                WorkflowErrorCodes.WorkflowNotFound,
                "A company is required to apply an industry preset (tax templates are company-scoped).");
        }

        // One transaction across catalogs (all repositories share the scoped DbContext, so the
        // nested calls join the ambient transaction instead of opening new ones): the tenant
        // gets the whole vertical or nothing.
        return await _workflows.ExecuteInTransactionAsync(async token =>
        {
            var customCreated = 0;
            var customSkipped = 0;
            foreach (var field in document.CustomFields)
            {
                var entity = (field.Entity ?? string.Empty).Trim();
                var name = (field.Field ?? string.Empty).Trim();
                if (entity.Length == 0 || name.Length == 0)
                {
                    customSkipped++;
                    continue;
                }

                var existing = await _customFields.GetActiveByEntityAsync(entity, token);
                if (existing.Any(d => string.Equals(d.FieldName, name, StringComparison.Ordinal)))
                {
                    customSkipped++;
                    continue;
                }

                await _customFields.AddAsync(new CustomFieldDefinition
                {
                    Id = Guid.NewGuid(),
                    EntityName = entity,
                    FieldName = name,
                    FieldType = NormalizeFieldType(field.Type),
                    IsRequired = field.Required,
                    DefaultValue = field.Default,
                    ValidationJsonSchema = field.Schema,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                }, token);
                customCreated++;
            }

            var workflowsCreated = 0;
            var workflowsSkipped = 0;
            foreach (var workflow in document.Workflows)
            {
                var existing = await _workflows.GetAllAsync(token);
                if (existing.Any(d =>
                    string.Equals(d.DocumentType, workflow.DocumentType, StringComparison.Ordinal)
                    && string.Equals(d.Name, workflow.Name, StringComparison.Ordinal)))
                {
                    workflowsSkipped++;
                    continue;
                }

                var definition = new WorkflowDefinition
                {
                    Id = Guid.NewGuid(),
                    DocumentType = (workflow.DocumentType ?? string.Empty).Trim(),
                    Name = (workflow.Name ?? string.Empty).Trim(),
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    States = workflow.States.Select(s => new WorkflowStateDefinition
                    {
                        Id = Guid.NewGuid(),
                        StateName = (s.Name ?? string.Empty).Trim(),
                        IsInitial = s.Initial,
                        CanSubmit = s.CanSubmit,
                    }).ToList<WorkflowStateDefinition>(),
                    Transitions = workflow.Transitions.Select(t => new WorkflowTransition
                    {
                        Id = Guid.NewGuid(),
                        FromState = (t.From ?? string.Empty).Trim(),
                        ToState = (t.To ?? string.Empty).Trim(),
                        ActionLabel = (t.Label ?? string.Empty).Trim(),
                        AllowedRole = (t.Role ?? string.Empty).Trim(),
                    }).ToList<WorkflowTransition>(),
                };

                WorkflowValidator.EnsureValidDefinition(
                    definition.States.Select(s => (s.StateName, s.IsInitial, s.CanSubmit)).ToList(),
                    definition.Transitions.Select(t => (t.FromState, t.ToState, t.ActionLabel, t.AllowedRole)).ToList());

                await DeactivateOthersAsync(definition.DocumentType, definition.Id, token);
                await _workflows.AddAsync(definition, token);
                workflowsCreated++;
            }

            var templatesCreated = 0;
            var templatesSkipped = 0;
            foreach (var template in document.TaxTemplates)
            {
                var title = (template.Title ?? string.Empty).Trim();
                var category = NormalizeCategory(template.Category);
                if (title.Length == 0)
                {
                    templatesSkipped++;
                    continue;
                }

                if (await _taxTemplates.ExistsTitleAsync(companyId, category, title, null, token))
                {
                    templatesSkipped++;
                    continue;
                }

                // Presets carry account CODES (ids are tenant-local): resolve each line against
                // the company's chart; unresolvable lines are skipped, never fabricated. A
                // template with zero resolvable lines is skipped as a whole.
                var lines = new List<TaxTemplateItem>();
                foreach (var line in template.Lines)
                {
                    var matches = await _accounts.FindActiveLeafByCodeAsync(
                        companyId, (line.AccountCode ?? string.Empty).Trim(), token);
                    if (matches.Count != 1)
                    {
                        continue;
                    }

                    if (!TryParseChargeType(line.ChargeType, out var chargeType)
                        || !TryParseBasis(line.Basis, out var basis))
                    {
                        continue;
                    }

                    if (basis != TaxCalculationBasis.FixedAmount && (line.Rate < 0 || line.Rate > 100))
                    {
                        continue;
                    }

                    lines.Add(new TaxTemplateItem
                    {
                        Id = Guid.NewGuid(),
                        AccountId = matches[0].Id,
                        Description = (line.Description ?? string.Empty).Trim(),
                        Rate = line.Rate,
                        Amount = line.Amount,
                        ChargeType = chargeType,
                        CalculationBasis = basis,
                    });
                }

                if (lines.Count == 0)
                {
                    templatesSkipped++;
                    continue;
                }

                await _taxTemplates.AddAsync(new TaxTemplate
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    Title = title,
                    Category = category,
                    Taxes = lines,
                }, token);
                templatesCreated++;
            }

            await _tenantPresets.SetAsync(document.Key, token);

            return new PresetApplyResult(
                document.Key,
                customCreated, customSkipped,
                workflowsCreated, workflowsSkipped,
                templatesCreated, templatesSkipped);
        }, cancellationToken);
    }

    public async Task<ActivePresetDto> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var record = await _tenantPresets.GetAsync(cancellationToken);
        if (record is null)
        {
            return new ActivePresetDto(null, null, new Dictionary<string, IReadOnlyDictionary<string, string>>());
        }

        var document = Load(record.PresetKey);
        return new ActivePresetDto(
            document.Key,
            document.Name,
            document.Terminology.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyDictionary<string, string>)kvp.Value
                    .SelectMany(ns => ns.Value.Select(term => new KeyValuePair<string, string>($"{ns.Key}:{term.Key}", term.Value)))
                    .ToDictionary(p => p.Key, p => p.Value)));
    }

    private static string NormalizeFieldType(string? type)
    {
        var normalized = (type ?? "string").Trim().ToLowerInvariant();
        return normalized switch
        {
            "string" or "number" or "boolean" or "date" or "select" => normalized,
            _ => "string",
        };
    }

    private static string NormalizeCategory(string? category)
    {
        var normalized = (category ?? "Sales").Trim();
        return string.Equals(normalized, "Purchase", StringComparison.OrdinalIgnoreCase)
            ? "Purchase"
            : "Sales";
    }

    private async Task DeactivateOthersAsync(string documentType, Guid keepId, CancellationToken ct)
    {
        var all = await _workflows.GetAllAsync(ct);
        foreach (var other in all.Where(d => d.DocumentType == documentType && d.Id != keepId && d.IsActive))
        {
            other.IsActive = false;
            await _workflows.UpdateAsync(other, ct);
        }
    }

    /// <summary>
    /// Best-effort account resolution across the tenant's companies (presets carry codes, not
    /// ids): the first company owning an active leaf with the code wins. Unresolvable lines
    /// are skipped, never fabricated.
    /// </summary>
    private static bool TryParseChargeType(string? value, out TaxChargeType chargeType)
    {
        if (Enum.TryParse<TaxChargeType>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            chargeType = parsed;
            return true;
        }

        chargeType = TaxChargeType.Add;
        return false;
    }

    private static bool TryParseBasis(string? value, out TaxCalculationBasis basis)
    {
        if (Enum.TryParse<TaxCalculationBasis>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            basis = parsed;
            return true;
        }

        basis = TaxCalculationBasis.OnNetTotal;
        return false;
    }
}
