using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// Workstation / machine center master (Task 9.1, plan.md §1 DDL table 1): equipment, machine or
/// bench where manufacturing operations occur, with configured hourly labor, electricity and rent
/// rates. The composite operating rate is a computed getter mirroring the DDL computed column
/// <c>HourRateTotal AS (HourRateLabor + HourRateElectricity + HourRateRent)</c>.
/// </summary>
public class Workstation : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public string WorkstationName { get; set; } = string.Empty;

    /// <summary>Hourly labor rate, decimal(18,4), must be &gt;= 0.</summary>
    public decimal HourRateLabor { get; set; }

    /// <summary>Hourly electricity rate, decimal(18,4), must be &gt;= 0.</summary>
    public decimal HourRateElectricity { get; set; }

    /// <summary>Hourly rent / overhead rate, decimal(18,4), must be &gt;= 0.</summary>
    public decimal HourRateRent { get; set; }

    /// <summary>
    /// Composite hourly operating cost (plan DDL computed column). Pure sum - exact decimal
    /// arithmetic, no rounding here; rounding happens at the cost-engine line level.
    /// </summary>
    /// <remarks>
    /// The private setter exists SOLELY so EF Core can materialize the computed column on query
    /// (a getter-only property fails model validation at runtime). It intentionally discards:
    /// the getter always recomputes from the live rates - single source of truth, never stale.
    /// </remarks>
    public decimal HourRateTotal
    {
        get => HourRateLabor + HourRateElectricity + HourRateRent;
        private set { }
    }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
