using System;
using System.Collections.Generic;
using AssetHub.Domain.Finance;

namespace AssetHub.Application.Finance.Dtos;

public record AssetFinanceBookDto(
    Guid Id,
    Guid AssetId,
    decimal AcquisitionCost,
    decimal ResidualValue,
    int UsefulLifeMonths,
    DepreciationMethod DepreciationMethod,
    decimal? DepreciationRatePct,
    int FrequencyMonths,
    DateTime StartDate,
    string Currency,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record UpsertFinanceBookRequest(
    decimal AcquisitionCost,
    decimal ResidualValue,
    int UsefulLifeMonths,
    DepreciationMethod DepreciationMethod,
    decimal? DepreciationRatePct,
    int FrequencyMonths,
    DateTime StartDate,
    string Currency = "MXN"
);

public record AssetDepreciationScheduleDto(
    Guid Id,
    int PeriodNumber,
    DateTime PeriodStartDate,
    DateTime PeriodEndDate,
    decimal ProjectedDepreciationAmount,
    decimal ProjectedAccumulatedDepreciation,
    decimal ProjectedNetBookValue,
    bool IsPosted,
    Guid? PostedEntryId
);

public record ManualScheduleItemDto(
    int PeriodNumber,
    DateTime PeriodStartDate,
    DateTime PeriodEndDate,
    decimal DepreciationAmount
);

public record GenerateScheduleRequest(
    bool ForceRegenerate = false,
    List<ManualScheduleItemDto>? ManualSchedule = null
);

public record GenerateScheduleResponse(
    int PeriodsGenerated,
    DateTime LastPeriodEndDate
);

public record RecalculateScheduleRequest(
    DateTime EffectiveFromDate,
    string Reason
);

public record RecalculateScheduleResponse(
    int PeriodsRecalculated,
    DateTime LastPeriodEndDate
);

public record AssetDepreciationEntryDto(
    Guid Id,
    int PeriodNumber,
    DateTime AccountingDate,
    decimal DepreciationAmount,
    decimal AccumulatedDepreciation,
    decimal NetBookValue,
    string IdempotencyKey,
    Guid PostedBy,
    DateTime PostedAt,
    string? Notes
);

public record PostDepreciationRequest(
    int PeriodsToPost,
    DateTime? AccountingDate = null,
    string? Notes = null
);

public record PostDepreciationResponse(
    List<AssetDepreciationEntryDto> PostedEntries,
    int PeriodsPosted,
    decimal NewNetBookValue
);

public record AssetValueAdjustmentDto(
    Guid Id,
    ValueAdjustmentType AdjustmentType,
    decimal PreviousNetBookValue,
    decimal AdjustmentAmount,
    decimal NewNetBookValue,
    string Reason,
    DateTime EffectiveDate,
    Guid ApprovedBy,
    DateTime ApprovedAt
);

public record CreateValueAdjustmentRequest(
    ValueAdjustmentType AdjustmentType,
    decimal AdjustmentAmount,
    string Reason,
    DateTime EffectiveDate
);

public record AssetDisposalDto(
    Guid Id,
    DisposalType DisposalType,
    DateTime DisposalDate,
    decimal NetBookValueAtDisposal,
    decimal ProceedsAmount,
    decimal GainLossAmount,
    string Reason,
    string? DocumentReference,
    Guid ApprovedBy,
    DateTime ApprovedAt
);

public record CreateDisposalRequest(
    DisposalType DisposalType,
    DateTime DisposalDate,
    decimal ProceedsAmount,
    string Reason,
    string? DocumentReference
);

public record AssetCustodyTransferDto(
    Guid Id,
    Guid? FromEmployeeId,
    string? FromEmployeeName,
    Guid ToEmployeeId,
    string ToEmployeeName,
    Guid? FromDepartmentId,
    string? FromDepartmentName,
    Guid? ToDepartmentId,
    string? ToDepartmentName,
    DateTime TransferDate,
    CustodyTransferType TransferType,
    string Reason,
    string? DocumentUrl,
    Guid? SignedByFrom,
    Guid? SignedByTo,
    Guid CreatedBy,
    DateTime CreatedAt
);

public record CreateCustodyTransferRequest(
    Guid ToEmployeeId,
    Guid? FromDepartmentId,
    Guid? ToDepartmentId,
    DateTime TransferDate,
    CustodyTransferType TransferType,
    string Reason,
    string? DocumentUrl,
    Guid? SignedByFrom,
    Guid? SignedByTo
);

public record AssetRepairCapitalizationDto(
    Guid Id,
    Guid MaintenanceOrderId,
    decimal CapitalizedAmount,
    int? NewUsefulLifeMonths,
    DateTime EffectiveDate,
    Guid ApprovedBy,
    DateTime ApprovedAt
);

public record CapitalizeMaintenanceRequest(
    int? NewUsefulLifeMonths,
    DateTime EffectiveDate
);

public record AssetFinanceSummaryDto(
    Guid AssetId,
    string AssetCode,
    string AssetName,
    decimal? AcquisitionCost,
    decimal? ResidualValue,
    int? UsefulLifeMonths,
    DepreciationMethod? DepreciationMethod,
    decimal CurrentNetBookValue,
    decimal AccumulatedDepreciation,
    decimal DepreciationThisPeriod,
    DateTime? NextDepreciationDate,
    int RemainingPeriods,
    bool HasFinanceBook,
    bool IsDisposed,
    string? DisposalType,
    DateTime? DisposalDate
);