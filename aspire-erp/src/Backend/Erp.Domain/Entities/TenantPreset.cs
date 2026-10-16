using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Records which industry preset a tenant runs (workflow Fase 5, gap D3): the applied
/// preset key plus when it was applied. The preset's rows (custom fields, workflows, tax
/// templates) live as ordinary catalog rows after application - editable and deletable
/// individually. Terminology is served live from the preset's embedded JSON, so this
/// record is also what the overlay endpoint reads.
/// </summary>
public class TenantPreset : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Preset key, e.g. "healthcare" (matches the embedded JSON file).</summary>
    public string PresetKey { get; set; } = string.Empty;

    public DateTimeOffset AppliedAt { get; set; }
}
