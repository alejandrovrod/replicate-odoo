using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Common.Models;
using AssetHub.Application.Finance.Dtos;
using AssetHub.Application.Finance.Queries;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Finance;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Finance.Handlers.Queries;

public class GetFinanceProfileQueryHandler : IRequestHandler<GetFinanceProfileQuery, AssetFinanceBookDto?>
{
    private readonly ITenantDbContext _dbContext;
    private readonly IMapper _mapper;

    public GetFinanceProfileQueryHandler(ITenantDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<AssetFinanceBookDto?> Handle(GetFinanceProfileQuery request, CancellationToken cancellationToken)
    {
        var book = await _dbContext.AssetFinanceBooks
            .Where(b => b.AssetId == request.AssetId && !b.IsDeleted)
            .ProjectTo<AssetFinanceBookDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(cancellationToken);

        return book;
    }
}

public class GetDepreciationSchedulesQueryHandler : IRequestHandler<GetDepreciationSchedulesQuery, PagedResult<AssetDepreciationScheduleDto>>
{
    private readonly ITenantDbContext _dbContext;
    private readonly IMapper _mapper;

    public GetDepreciationSchedulesQueryHandler(ITenantDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<PagedResult<AssetDepreciationScheduleDto>> Handle(GetDepreciationSchedulesQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.AssetDepreciationSchedules
            .Where(s => s.AssetId == request.AssetId && !s.IsDeleted);

        if (request.OnlyPending == true)
        {
            query = query.Where(s => !s.IsPosted);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(s => s.PeriodNumber)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ProjectTo<AssetDepreciationScheduleDto>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        return new PagedResult<AssetDepreciationScheduleDto>(items, totalCount, request.Page, request.PageSize);
    }
}

public class GetDepreciationEntriesQueryHandler : IRequestHandler<GetDepreciationEntriesQuery, PagedResult<AssetDepreciationEntryDto>>
{
    private readonly ITenantDbContext _dbContext;
    private readonly IMapper _mapper;

    public GetDepreciationEntriesQueryHandler(ITenantDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<PagedResult<AssetDepreciationEntryDto>> Handle(GetDepreciationEntriesQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.AssetDepreciationEntries
            .Where(e => e.AssetId == request.AssetId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.PeriodNumber)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ProjectTo<AssetDepreciationEntryDto>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        return new PagedResult<AssetDepreciationEntryDto>(items, totalCount, request.Page, request.PageSize);
    }
}

public class GetValueAdjustmentsQueryHandler : IRequestHandler<GetValueAdjustmentsQuery, PagedResult<AssetValueAdjustmentDto>>
{
    private readonly ITenantDbContext _dbContext;
    private readonly IMapper _mapper;

    public GetValueAdjustmentsQueryHandler(ITenantDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<PagedResult<AssetValueAdjustmentDto>> Handle(GetValueAdjustmentsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.AssetValueAdjustments
            .Where(a => a.AssetId == request.AssetId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.EffectiveDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ProjectTo<AssetValueAdjustmentDto>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        return new PagedResult<AssetValueAdjustmentDto>(items, totalCount, request.Page, request.PageSize);
    }
}

public class GetDisposalQueryHandler : IRequestHandler<GetDisposalQuery, AssetDisposalDto?>
{
    private readonly ITenantDbContext _dbContext;
    private readonly IMapper _mapper;

    public GetDisposalQueryHandler(ITenantDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<AssetDisposalDto?> Handle(GetDisposalQuery request, CancellationToken cancellationToken)
    {
        var disposal = await _dbContext.AssetDisposals
            .Where(d => d.AssetId == request.AssetId)
            .ProjectTo<AssetDisposalDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(cancellationToken);

        return disposal;
    }
}

public class GetCustodyTransfersQueryHandler : IRequestHandler<GetCustodyTransfersQuery, PagedResult<AssetCustodyTransferDto>>
{
    private readonly ITenantDbContext _dbContext;
    private readonly IMapper _mapper;

    public GetCustodyTransfersQueryHandler(ITenantDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<PagedResult<AssetCustodyTransferDto>> Handle(GetCustodyTransfersQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.AssetCustodyTransfers
            .Where(t => t.AssetId == request.AssetId && !t.IsDeleted);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.TransferDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ProjectTo<AssetCustodyTransferDto>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        return new PagedResult<AssetCustodyTransferDto>(items, totalCount, request.Page, request.PageSize);
    }
}

public class GetFinanceSummaryQueryHandler : IRequestHandler<GetFinanceSummaryQuery, AssetFinanceSummaryDto?>
{
    private readonly ITenantDbContext _dbContext;
    private readonly IMapper _mapper;

    public GetFinanceSummaryQueryHandler(ITenantDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<AssetFinanceSummaryDto?> Handle(GetFinanceSummaryQuery request, CancellationToken cancellationToken)
    {
        var asset = await _dbContext.Assets
            .Where(a => a.Id == request.AssetId && !a.IsDeleted)
            .Select(a => new { a.Id, a.Code, a.Name, a.State })
            .FirstOrDefaultAsync(cancellationToken);

        if (asset == null) return null;

        var book = await _dbContext.AssetFinanceBooks
            .Where(b => b.AssetId == request.AssetId && !b.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (book == null)
        {
            return new AssetFinanceSummaryDto(
                asset.Id, asset.Code, asset.Name,
                null, null, null, null,
                0, 0, 0, null, 0,
                false, asset.State is "Disposed" or "Scrapped",
                asset.State, null
            );
        }

        var postedEntries = await _dbContext.AssetDepreciationEntries
            .Where(e => e.AssetId == request.AssetId)
            .OrderByDescending(e => e.PeriodNumber)
            .ToListAsync(cancellationToken);

        var accumulatedDepreciation = postedEntries.Sum(e => e.DepreciationAmount);
        var totalAdjustments = await _dbContext.AssetValueAdjustments
            .Where(a => a.AssetId == request.AssetId)
            .SumAsync(a => a.AdjustmentAmount, cancellationToken);
        var currentNetBookValue = book.AcquisitionCost - accumulatedDepreciation + totalAdjustments;
        var lastEntry = postedEntries.FirstOrDefault();
        var depreciationThisPeriod = lastEntry?.DepreciationAmount ?? 0;

        var nextSchedule = await _dbContext.AssetDepreciationSchedules
            .Where(s => s.AssetId == request.AssetId && !s.IsPosted && !s.IsDeleted)
            .OrderBy(s => s.PeriodNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var remainingPeriods = await _dbContext.AssetDepreciationSchedules
            .CountAsync(s => s.AssetId == request.AssetId && !s.IsPosted && !s.IsDeleted, cancellationToken);

        var disposal = await _dbContext.AssetDisposals
            .Where(d => d.AssetId == request.AssetId)
            .FirstOrDefaultAsync(cancellationToken);

        return new AssetFinanceSummaryDto(
            asset.Id,
            asset.Code,
            asset.Name,
            book.AcquisitionCost,
            book.ResidualValue,
            book.UsefulLifeMonths,
            book.DepreciationMethod,
            currentNetBookValue,
            accumulatedDepreciation,
            depreciationThisPeriod,
            nextSchedule?.PeriodStartDate,
            remainingPeriods,
            true,
            disposal != null,
            disposal?.DisposalType.ToString(),
            disposal?.DisposalDate
        );
    }
}