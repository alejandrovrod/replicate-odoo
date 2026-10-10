using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Finance.Commands;
using AssetHub.Application.Finance.Dtos;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Assets;
using AssetHub.Domain.Finance;
using AssetHub.Domain.Exceptions;
using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Finance.Handlers.Commands;

public class GenerateDepreciationScheduleCommandHandler : IRequestHandler<GenerateDepreciationScheduleCommand, GenerateScheduleResponse>
{
    private readonly ITenantDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly ITenantResolver _tenantResolver;

    public GenerateDepreciationScheduleCommandHandler(ITenantDbContext dbContext, IMapper mapper, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _tenantResolver = tenantResolver;
    }

    public async Task<GenerateScheduleResponse> Handle(GenerateDepreciationScheduleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId().Value;

        var book = await _dbContext.AssetFinanceBooks
            .FirstOrDefaultAsync(b => b.AssetId == request.AssetId && b.TenantId == tenantId && !b.IsDeleted, cancellationToken);
        if (book == null) throw new DomainException("finance_book_not_found", "Perfil financiero no configurado");

        var asset = await _dbContext.Assets
            .FirstOrDefaultAsync(a => a.Id == request.AssetId && a.TenantId == tenantId && !a.IsDeleted, cancellationToken);
        if (asset == null) throw new DomainException("asset_not_found", "Activo no encontrado");

        if (asset.State is "Disposed" or "Scrapped")
            throw new DomainException("asset_disposed", "No se puede generar schedule en activo dado de baja");

        var existingSchedules = await _dbContext.AssetDepreciationSchedules
            .Where(s => s.AssetId == request.AssetId && !s.IsDeleted)
            .ToListAsync(cancellationToken);

        var hasPosted = existingSchedules.Any(s => s.IsPosted);
        if (hasPosted && !request.Request.ForceRegenerate)
            throw new DomainException("has_posted_entries", "Existen cuotas devengadas. Use ForceRegenerate=true para regenerar (eliminará cuotas futuras no devengadas)", "Domain.HasPostedEntriesRegenerate");

        if (book.DepreciationMethod == DepreciationMethod.Manual)
        {
            if (request.Request.ManualSchedule == null || !request.Request.ManualSchedule.Any())
                throw new DomainException("manual_schedule_required", "El método Manual requiere ManualSchedule con cuotas personalizadas");

            return await GenerateManualSchedule(book, request.Request.ManualSchedule!, cancellationToken);
        }

        if (existingSchedules.Any() && request.Request.ForceRegenerate)
        {
            var futureSchedules = existingSchedules.Where(s => !s.IsPosted).ToList();
            _dbContext.AssetDepreciationSchedules.RemoveRange(futureSchedules);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var schedules = GenerateSystematicSchedule(book);
        var lastPeriod = schedules.LastOrDefault();

        _dbContext.AssetDepreciationSchedules.AddRange(schedules);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new GenerateScheduleResponse(schedules.Count, lastPeriod?.PeriodEndDate ?? book.StartDate);
    }

    private List<AssetDepreciationSchedule> GenerateSystematicSchedule(AssetFinanceBook book)
    {
        var schedules = new List<AssetDepreciationSchedule>();
        var depreciableBase = book.AcquisitionCost - book.ResidualValue;
        var totalPeriods = book.UsefulLifeMonths / book.FrequencyMonths;
        var currentDate = book.StartDate;
        var accumulatedDepreciation = 0m;
        var currentNetBookValue = book.AcquisitionCost;

        double ddbRate = 0;
        if (book.DepreciationMethod == DepreciationMethod.DoubleDeclining)
        {
            ddbRate = 2.0 / book.UsefulLifeMonths;
        }

        decimal wdvRate = 0;
        if (book.DepreciationMethod == DepreciationMethod.WrittenDownValue && book.DepreciationRatePct.HasValue)
        {
            wdvRate = book.DepreciationRatePct.Value / 100m / (12 / book.FrequencyMonths);
        }

        decimal straightLineAmount = 0;
        if (book.DepreciationMethod == DepreciationMethod.StraightLine)
        {
            straightLineAmount = Math.Round(depreciableBase / totalPeriods, 4);
        }

        for (int period = 1; period <= totalPeriods; period++)
        {
            var periodStart = currentDate;
            var periodEnd = currentDate.AddMonths(book.FrequencyMonths).AddDays(-1);

            decimal depreciationAmount = 0;

            switch (book.DepreciationMethod)
            {
                case DepreciationMethod.StraightLine:
                    depreciationAmount = straightLineAmount;
                    if (period == totalPeriods)
                    {
                        depreciationAmount = Math.Max(0, currentNetBookValue - book.ResidualValue);
                    }
                    break;

                case DepreciationMethod.DoubleDeclining:
                    depreciationAmount = Math.Round(currentNetBookValue * (decimal)ddbRate * book.FrequencyMonths, 4);
                    depreciationAmount = Math.Min(depreciationAmount, Math.Max(0, currentNetBookValue - book.ResidualValue));
                    break;

                case DepreciationMethod.WrittenDownValue:
                    depreciationAmount = Math.Round(currentNetBookValue * wdvRate, 4);
                    depreciationAmount = Math.Min(depreciationAmount, Math.Max(0, currentNetBookValue - book.ResidualValue));
                    break;
            }

            depreciationAmount = Math.Max(0, depreciationAmount);
            accumulatedDepreciation += depreciationAmount;
            currentNetBookValue = book.AcquisitionCost - accumulatedDepreciation;
            currentNetBookValue = Math.Max(currentNetBookValue, book.ResidualValue);

            schedules.Add(new AssetDepreciationSchedule
            {
                Id = Guid.NewGuid(),
                TenantId = book.TenantId,
                AssetId = book.AssetId,
                FinanceBookId = book.Id,
                PeriodNumber = period,
                PeriodStartDate = periodStart,
                PeriodEndDate = periodEnd,
                ProjectedDepreciationAmount = depreciationAmount,
                ProjectedAccumulatedDepreciation = accumulatedDepreciation,
                ProjectedNetBookValue = currentNetBookValue,
                IsPosted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            currentDate = currentDate.AddMonths(book.FrequencyMonths);
        }

        return schedules;
    }

    private async Task<GenerateScheduleResponse> GenerateManualSchedule(AssetFinanceBook book, List<ManualScheduleItemDto> manualItems, CancellationToken cancellationToken)
    {
        var schedules = new List<AssetDepreciationSchedule>();
        var accumulated = 0m;
        var currentNetBookValue = book.AcquisitionCost;

        foreach (var item in manualItems.OrderBy(m => m.PeriodNumber))
        {
            if (item.DepreciationAmount < 0)
                throw new DomainException("negative_depreciation", "Las cuotas manuales no pueden ser negativas");

            accumulated += item.DepreciationAmount;
            currentNetBookValue = book.AcquisitionCost - accumulated;

            if (currentNetBookValue < book.ResidualValue)
                throw new DomainException("below_residual", $"Período {item.PeriodNumber}: valor neto ({currentNetBookValue}) bajo residual ({book.ResidualValue})", "Domain.BelowResidual", item.PeriodNumber, currentNetBookValue, book.ResidualValue);

            schedules.Add(new AssetDepreciationSchedule
            {
                Id = Guid.NewGuid(),
                TenantId = book.TenantId,
                AssetId = book.AssetId,
                FinanceBookId = book.Id,
                PeriodNumber = item.PeriodNumber,
                PeriodStartDate = item.PeriodStartDate,
                PeriodEndDate = item.PeriodEndDate,
                ProjectedDepreciationAmount = item.DepreciationAmount,
                ProjectedAccumulatedDepreciation = accumulated,
                ProjectedNetBookValue = currentNetBookValue,
                IsPosted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        var totalDepreciation = schedules.Sum(s => s.ProjectedDepreciationAmount);
        var expectedDepreciation = book.AcquisitionCost - book.ResidualValue;
        if (Math.Abs(totalDepreciation - expectedDepreciation) > 0.01m)
            throw new DomainException("manual_total_mismatch", $"Suma cuotas manuales ({totalDepreciation}) != base depreciable ({expectedDepreciation})", "Domain.ManualTotalMismatch", totalDepreciation, expectedDepreciation);

        _dbContext.AssetDepreciationSchedules.AddRange(schedules);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new GenerateScheduleResponse(schedules.Count, schedules.Last().PeriodEndDate);
    }
}

public class RecalculateDepreciationScheduleCommandHandler : IRequestHandler<RecalculateDepreciationScheduleCommand, RecalculateScheduleResponse>
{
    private readonly ITenantDbContext _dbContext;
    private readonly ITenantResolver _tenantResolver;

    public RecalculateDepreciationScheduleCommandHandler(ITenantDbContext dbContext, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _tenantResolver = tenantResolver;
    }

    public async Task<RecalculateScheduleResponse> Handle(RecalculateDepreciationScheduleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId().Value;

        var book = await _dbContext.AssetFinanceBooks
            .FirstOrDefaultAsync(b => b.AssetId == request.AssetId && b.TenantId == tenantId && !b.IsDeleted, cancellationToken);
        if (book == null) throw new DomainException("finance_book_not_found", "Perfil financiero no configurado");

        var futureSchedules = await _dbContext.AssetDepreciationSchedules
            .Where(s => s.AssetId == request.AssetId && !s.IsPosted && !s.IsDeleted && s.PeriodStartDate >= request.Request.EffectiveFromDate)
            .OrderBy(s => s.PeriodNumber)
            .ToListAsync(cancellationToken);

        if (!futureSchedules.Any())
            throw new DomainException("no_future_periods", "No hay períodos futuros para recalcular desde la fecha efectiva");

        var lastPostedEntry = await _dbContext.AssetDepreciationEntries
            .Where(e => e.AssetId == request.AssetId)
            .OrderByDescending(e => e.PeriodNumber)
            .FirstOrDefaultAsync(cancellationToken);

        decimal currentNetBookValue = lastPostedEntry?.NetBookValue ?? book.AcquisitionCost;
        var depreciableRemaining = currentNetBookValue - book.ResidualValue;

        if (depreciableRemaining <= 0)
        {
            foreach (var s in futureSchedules)
            {
                s.ProjectedDepreciationAmount = 0;
                s.ProjectedAccumulatedDepreciation = book.AcquisitionCost - book.ResidualValue;
                s.ProjectedNetBookValue = book.ResidualValue;
                s.UpdatedAt = DateTime.UtcNow;
            }
        }
        else
        {
            var remainingPeriods = futureSchedules.Count;
            decimal amountPerPeriod = 0;

            switch (book.DepreciationMethod)
            {
                case DepreciationMethod.StraightLine:
                    amountPerPeriod = Math.Round(depreciableRemaining / remainingPeriods, 4);
                    break;

                case DepreciationMethod.DoubleDeclining:
                    var ddbRate = 2.0 / book.UsefulLifeMonths;
                    break;

                case DepreciationMethod.WrittenDownValue:
                    var wdvRate = book.DepreciationRatePct!.Value / 100m / (12 / book.FrequencyMonths);
                    break;
            }

            decimal accumulated = book.AcquisitionCost - currentNetBookValue;

            for (int i = 0; i < futureSchedules.Count; i++)
            {
                var s = futureSchedules[i];
                decimal depreciationAmount = 0;

                switch (book.DepreciationMethod)
                {
                    case DepreciationMethod.StraightLine:
                        depreciationAmount = amountPerPeriod;
                        if (i == futureSchedules.Count - 1)
                            depreciationAmount = Math.Max(0, currentNetBookValue - book.ResidualValue);
                        break;

                    case DepreciationMethod.DoubleDeclining:
                        depreciationAmount = Math.Round(currentNetBookValue * (decimal)(2.0 / book.UsefulLifeMonths) * book.FrequencyMonths, 4);
                        depreciationAmount = Math.Min(depreciationAmount, Math.Max(0, currentNetBookValue - book.ResidualValue));
                        break;

                    case DepreciationMethod.WrittenDownValue:
                        var wdvRate = book.DepreciationRatePct!.Value / 100m / (12 / book.FrequencyMonths);
                        depreciationAmount = Math.Round(currentNetBookValue * wdvRate, 4);
                        depreciationAmount = Math.Min(depreciationAmount, Math.Max(0, currentNetBookValue - book.ResidualValue));
                        break;
                }

                depreciationAmount = Math.Max(0, depreciationAmount);
                accumulated += depreciationAmount;
                currentNetBookValue = book.AcquisitionCost - accumulated;
                currentNetBookValue = Math.Max(currentNetBookValue, book.ResidualValue);

                s.ProjectedDepreciationAmount = depreciationAmount;
                s.ProjectedAccumulatedDepreciation = accumulated;
                s.ProjectedNetBookValue = currentNetBookValue;
                s.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RecalculateScheduleResponse(futureSchedules.Count, futureSchedules.Last().PeriodEndDate);
    }
}