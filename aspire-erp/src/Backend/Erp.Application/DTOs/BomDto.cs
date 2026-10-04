namespace Erp.Application.DTOs;

/// <summary>One component line of a BOM (Task 9.5 tree reads).</summary>
public sealed record BomItemDto(
    Guid Id,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    decimal Quantity,
    decimal ValuationRate,
    decimal Amount,
    decimal ScrapPercentage);

/// <summary>One workstation operation of a BOM, with its workstation's composite hourly rate.</summary>
public sealed record BomOperationDto(
    Guid Id,
    Guid WorkstationId,
    string WorkstationName,
    decimal HourRateTotal,
    string? Description,
    decimal DurationMinutes,
    decimal OperationCost);

/// <summary>BOM payload (Task 9.5 tree reads): header + persisted cost roll-up + lines + operations.</summary>
public sealed record BomDto(
    Guid Id,
    Guid CompanyId,
    string BomNumber,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    decimal Quantity,
    bool IsActive,
    bool IsDefault,
    decimal RawMaterialCost,
    decimal OperatingCost,
    decimal ScrapCost,
    decimal TotalCost,
    IReadOnlyList<BomItemDto> Items,
    IReadOnlyList<BomOperationDto> Operations);
