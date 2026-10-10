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

public class PostDepreciationEntriesCommandHandler : IRequestHandler<PostDepreciationEntriesCommand, PostDepreciationResponse>
{
    private readonly ITenantDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly ITenantResolver _tenantResolver;

    public PostDepreciationEntriesCommandHandler(ITenantDbContext dbContext, IMapper mapper, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _tenantResolver = tenantResolver;
    }

    public async Task<PostDepreciationResponse> Handle(PostDepreciationEntriesCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId().Value;

        var book = await _dbContext.AssetFinanceBooks
            .FirstOrDefaultAsync(b => b.AssetId == request.AssetId && b.TenantId == tenantId && !b.IsDeleted, cancellationToken);
        if (book == null) throw new DomainException("finance_book_not_found", "Perfil financiero no configurado");

        var asset = await _dbContext.Assets
            .FirstOrDefaultAsync(a => a.Id == request.AssetId && a.TenantId == tenantId && !a.IsDeleted, cancellationToken);
        if (asset == null) throw new DomainException("asset_not_found", "Activo no encontrado");

        if (asset.State is "Disposed" or "Scrapped")
            throw new DomainException("asset_disposed", "No se pueden devengar cuotas en activo dado de baja");

        var disposal = await _dbContext.AssetDisposals
            .FirstOrDefaultAsync(d => d.AssetId == request.AssetId, cancellationToken);
        if (disposal != null)
            throw new DomainException("asset_disposed", "El activo tiene baja registrada");

        var pendingSchedules = await _dbContext.AssetDepreciationSchedules
            .Where(s => s.AssetId == request.AssetId && !s.IsPosted && !s.IsDeleted)
            .OrderBy(s => s.PeriodNumber)
            .Take(request.Request.PeriodsToPost)
            .ToListAsync(cancellationToken);

        if (!pendingSchedules.Any())
            throw new DomainException("no_pending_periods", "No hay cuotas pendientes por devengar");

        var accountingDate = request.Request.AccountingDate ?? DateTime.UtcNow;
        var postedEntries = new List<AssetDepreciationEntry>();
        var lastPostedEntry = await _dbContext.AssetDepreciationEntries
            .Where(e => e.AssetId == request.AssetId)
            .OrderByDescending(e => e.PeriodNumber)
            .FirstOrDefaultAsync(cancellationToken);

        decimal accumulatedDepreciation = lastPostedEntry?.AccumulatedDepreciation ?? 0;
        decimal currentNetBookValue = lastPostedEntry?.NetBookValue ?? book.AcquisitionCost;

        foreach (var schedule in pendingSchedules)
        {
            var idempotencyKey = $"{request.AssetId}:{schedule.PeriodNumber}:{book.FrequencyMonths}";

            var existingEntry = await _dbContext.AssetDepreciationEntries
                .FirstOrDefaultAsync(e => e.IdempotencyKey == idempotencyKey, cancellationToken);

            if (existingEntry != null)
            {
                postedEntries.Add(existingEntry);
                continue;
            }

            var depreciationAmount = schedule.ProjectedDepreciationAmount;
            accumulatedDepreciation += depreciationAmount;
            currentNetBookValue = book.AcquisitionCost - accumulatedDepreciation;
            currentNetBookValue = Math.Max(currentNetBookValue, book.ResidualValue);

            var entry = new AssetDepreciationEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AssetId = request.AssetId,
                FinanceBookId = book.Id,
                ScheduleId = schedule.Id,
                PeriodNumber = schedule.PeriodNumber,
                AccountingDate = accountingDate,
                DepreciationAmount = depreciationAmount,
                AccumulatedDepreciation = accumulatedDepreciation,
                NetBookValue = currentNetBookValue,
                IdempotencyKey = idempotencyKey,
                PostedBy = Guid.Empty, // TODO: obtener del contexto de auth
                PostedAt = DateTime.UtcNow,
                Notes = request.Request.Notes,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.AssetDepreciationEntries.Add(entry);

            schedule.IsPosted = true;
            schedule.PostedEntryId = entry.Id;
            schedule.UpdatedAt = DateTime.UtcNow;

            postedEntries.Add(entry);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var dtoEntries = _mapper.Map<List<AssetDepreciationEntryDto>>(postedEntries);

        return new PostDepreciationResponse(dtoEntries, postedEntries.Count, currentNetBookValue);
    }
}