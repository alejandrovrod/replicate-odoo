using System;
using MediatR;
using AssetHub.Application.Finance.Dtos;
using AssetHub.Domain.Finance;

namespace AssetHub.Application.Finance.Commands;

public record UpsertFinanceBookCommand(
    Guid AssetId,
    UpsertFinanceBookRequest Request
) : IRequest<AssetFinanceBookDto>;

public record DeleteFinanceBookCommand(Guid AssetId) : IRequest<bool>;

public record GenerateDepreciationScheduleCommand(
    Guid AssetId,
    GenerateScheduleRequest Request
) : IRequest<GenerateScheduleResponse>;

public record RecalculateDepreciationScheduleCommand(
    Guid AssetId,
    RecalculateScheduleRequest Request
) : IRequest<RecalculateScheduleResponse>;

public record PostDepreciationEntriesCommand(
    Guid AssetId,
    PostDepreciationRequest Request
) : IRequest<PostDepreciationResponse>;

public record CreateValueAdjustmentCommand(
    Guid AssetId,
    CreateValueAdjustmentRequest Request
) : IRequest<AssetValueAdjustmentDto>;

public record CreateDisposalCommand(
    Guid AssetId,
    CreateDisposalRequest Request
) : IRequest<AssetDisposalDto>;

public record CreateCustodyTransferCommand(
    Guid AssetId,
    CreateCustodyTransferRequest Request
) : IRequest<AssetCustodyTransferDto>;

public record CapitalizeMaintenanceOrderCommand(
    Guid MaintenanceOrderId,
    CapitalizeMaintenanceRequest Request
) : IRequest<AssetRepairCapitalizationDto>;