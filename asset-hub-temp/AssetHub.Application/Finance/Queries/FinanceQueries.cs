using System;
using MediatR;
using AssetHub.Application.Common.Models;
using AssetHub.Application.Finance.Dtos;

namespace AssetHub.Application.Finance.Queries;

public record GetFinanceProfileQuery(Guid AssetId) : IRequest<AssetFinanceBookDto?>;

public record GetDepreciationSchedulesQuery(
    Guid AssetId,
    int Page = 1,
    int PageSize = 50,
    bool? OnlyPending = null
) : IRequest<PagedResult<AssetDepreciationScheduleDto>>;

public record GetDepreciationEntriesQuery(
    Guid AssetId,
    int Page = 1,
    int PageSize = 50
) : IRequest<PagedResult<AssetDepreciationEntryDto>>;

public record GetValueAdjustmentsQuery(
    Guid AssetId,
    int Page = 1,
    int PageSize = 50
) : IRequest<PagedResult<AssetValueAdjustmentDto>>;

public record GetDisposalQuery(Guid AssetId) : IRequest<AssetDisposalDto?>;

public record GetCustodyTransfersQuery(
    Guid AssetId,
    int Page = 1,
    int PageSize = 50
) : IRequest<PagedResult<AssetCustodyTransferDto>>;

public record GetFinanceSummaryQuery(Guid AssetId) : IRequest<AssetFinanceSummaryDto?>;