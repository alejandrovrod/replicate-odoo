using System;
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

public class CreateValueAdjustmentCommandHandler : IRequestHandler<CreateValueAdjustmentCommand, AssetValueAdjustmentDto>
{
    private readonly ITenantDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly ITenantResolver _tenantResolver;

    public CreateValueAdjustmentCommandHandler(ITenantDbContext dbContext, IMapper mapper, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _tenantResolver = tenantResolver;
    }

    public async Task<AssetValueAdjustmentDto> Handle(CreateValueAdjustmentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId().Value;

        var book = await _dbContext.AssetFinanceBooks
            .FirstOrDefaultAsync(b => b.AssetId == request.AssetId && b.TenantId == tenantId && !b.IsDeleted, cancellationToken);
        if (book == null) throw new DomainException("finance_book_not_found", "Perfil financiero no configurado");

        var asset = await _dbContext.Assets
            .FirstOrDefaultAsync(a => a.Id == request.AssetId && a.TenantId == tenantId && !a.IsDeleted, cancellationToken);
        if (asset == null) throw new DomainException("asset_not_found", "Activo no encontrado");

        if (asset.State is "Disposed" or "Scrapped")
            throw new DomainException("asset_disposed", "No se pueden registrar ajustes en activo dado de baja");

        var disposal = await _dbContext.AssetDisposals
            .FirstOrDefaultAsync(d => d.AssetId == request.AssetId, cancellationToken);
        if (disposal != null)
            throw new DomainException("asset_disposed", "El activo tiene baja registrada");

        var req = request.Request;

        var lastPostedEntry = await _dbContext.AssetDepreciationEntries
            .Where(e => e.AssetId == request.AssetId)
            .OrderByDescending(e => e.PeriodNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var currentNetBookValue = lastPostedEntry?.NetBookValue ?? book.AcquisitionCost;
        var newNetBookValue = currentNetBookValue + req.AdjustmentAmount;

        if (newNetBookValue < book.ResidualValue)
            throw new DomainException("below_residual_value", $"El ajuste reduciría el valor neto ({newNetBookValue}) por debajo del residual ({book.ResidualValue})", "Domain.BelowResidualValue", newNetBookValue, book.ResidualValue);

        if (req.AdjustmentAmount > 0 && req.AdjustmentType == ValueAdjustmentType.Impairment)
            throw new DomainException("invalid_impairment", "El deterioro (Impairment) debe ser un monto negativo");

        if (req.AdjustmentAmount < 0 && req.AdjustmentType == ValueAdjustmentType.Revaluation)
            throw new DomainException("invalid_revaluation", "La revaluación debe ser un monto positivo");

        var adjustment = new AssetValueAdjustment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AssetId = request.AssetId,
            FinanceBookId = book.Id,
            AdjustmentType = req.AdjustmentType,
            PreviousNetBookValue = currentNetBookValue,
            AdjustmentAmount = req.AdjustmentAmount,
            NewNetBookValue = newNetBookValue,
            Reason = req.Reason,
            EffectiveDate = req.EffectiveDate,
            ApprovedBy = Guid.Empty, // TODO: contexto auth
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AssetValueAdjustments.Add(adjustment);

        await RecalculateFutureSchedules(book, newNetBookValue, req.EffectiveDate, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return _mapper.Map<AssetValueAdjustmentDto>(adjustment);
    }

    private async Task RecalculateFutureSchedules(AssetFinanceBook book, decimal newNetBookValue, DateTime effectiveDate, CancellationToken cancellationToken)
    {
        var futureSchedules = await _dbContext.AssetDepreciationSchedules
            .Where(s => s.AssetId == book.AssetId && !s.IsPosted && !s.IsDeleted && s.PeriodStartDate >= effectiveDate)
            .OrderBy(s => s.PeriodNumber)
            .ToListAsync(cancellationToken);

        if (!futureSchedules.Any()) return;

        var depreciableRemaining = newNetBookValue - book.ResidualValue;
        var remainingPeriods = futureSchedules.Count;
        decimal accumulated = book.AcquisitionCost - newNetBookValue;
        decimal currentNetBookValue = newNetBookValue;

        if (depreciableRemaining <= 0)
        {
            foreach (var s in futureSchedules)
            {
                s.ProjectedDepreciationAmount = 0;
                s.ProjectedAccumulatedDepreciation = book.AcquisitionCost - book.ResidualValue;
                s.ProjectedNetBookValue = book.ResidualValue;
                s.UpdatedAt = DateTime.UtcNow;
            }
            return;
        }

        for (int i = 0; i < futureSchedules.Count; i++)
        {
            var s = futureSchedules[i];
            decimal depreciationAmount = 0;

            switch (book.DepreciationMethod)
            {
                case DepreciationMethod.StraightLine:
                    var amountPerPeriod = Math.Round(depreciableRemaining / remainingPeriods, 4);
                    depreciationAmount = amountPerPeriod;
                    if (i == futureSchedules.Count - 1)
                        depreciationAmount = Math.Max(0, currentNetBookValue - book.ResidualValue);
                    break;

                case DepreciationMethod.DoubleDeclining:
                    var ddbRate = 2.0 / book.UsefulLifeMonths;
                    depreciationAmount = Math.Round(currentNetBookValue * (decimal)ddbRate * book.FrequencyMonths, 4);
                    depreciationAmount = Math.Min(depreciationAmount, Math.Max(0, currentNetBookValue - book.ResidualValue));
                    break;

                case DepreciationMethod.WrittenDownValue:
                    var wdvRate = book.DepreciationRatePct!.Value / 100m / (12 / book.FrequencyMonths);
                    depreciationAmount = Math.Round(currentNetBookValue * wdvRate, 4);
                    depreciationAmount = Math.Min(depreciationAmount, Math.Max(0, currentNetBookValue - book.ResidualValue));
                    break;

                case DepreciationMethod.Manual:
                    throw new DomainException("manual_recalc_not_supported", "Recálculo prospectivo no soportado para método Manual. Regenerar schedule completo.");
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
}