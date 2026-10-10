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
using AssetHub.Domain.Maintenance;
using AssetHub.Domain.Exceptions;
using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Finance.Handlers.Commands;

public class UpsertFinanceBookCommandHandler : IRequestHandler<UpsertFinanceBookCommand, AssetFinanceBookDto>
{
    private readonly ITenantDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly ITenantResolver _tenantResolver;

    public UpsertFinanceBookCommandHandler(ITenantDbContext dbContext, IMapper mapper, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _tenantResolver = tenantResolver;
    }

    public async Task<AssetFinanceBookDto> Handle(UpsertFinanceBookCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId().Value;

        var asset = await _dbContext.Assets
            .FirstOrDefaultAsync(a => a.Id == request.AssetId && a.TenantId == tenantId && !a.IsDeleted, cancellationToken);
        if (asset == null) throw new DomainException("asset_not_found", "Activo no encontrado");

        if (asset.State is "Disposed" or "Scrapped")
            throw new DomainException("asset_disposed", "No se puede configurar perfil financiero en activo dado de baja");

        var existing = await _dbContext.AssetFinanceBooks
            .FirstOrDefaultAsync(b => b.AssetId == request.AssetId && !b.IsDeleted, cancellationToken);

        var req = request.Request;

        if (req.ResidualValue > req.AcquisitionCost)
            throw new DomainException("residual_exceeds_acquisition", "El valor residual no puede superar el costo de adquisición");

        if (req.UsefulLifeMonths <= 0)
            throw new DomainException("invalid_useful_life", "La vida útil debe ser mayor a 0");

        if (req.FrequencyMonths <= 0)
            throw new DomainException("invalid_frequency", "La frecuencia debe ser mayor a 0");

        if (req.DepreciationMethod is DepreciationMethod.StraightLine or DepreciationMethod.DoubleDeclining)
        {
            if (req.UsefulLifeMonths % req.FrequencyMonths != 0)
                throw new DomainException("frequency_mismatch", "La frecuencia debe dividir exactamente la vida útil en meses");
        }

        if (req.DepreciationMethod == DepreciationMethod.WrittenDownValue && !req.DepreciationRatePct.HasValue)
            throw new DomainException("rate_required", "El método WrittenDownValue requiere DepreciationRatePct");

        if (req.DepreciationMethod == DepreciationMethod.Manual && req.FrequencyMonths != 1)
            throw new DomainException("manual_frequency", "El método Manual requiere frecuencia mensual (1)");

        if (existing != null)
        {
            existing.AcquisitionCost = req.AcquisitionCost;
            existing.ResidualValue = req.ResidualValue;
            existing.UsefulLifeMonths = req.UsefulLifeMonths;
            existing.DepreciationMethod = req.DepreciationMethod;
            existing.DepreciationRatePct = req.DepreciationRatePct;
            existing.FrequencyMonths = req.FrequencyMonths;
            existing.StartDate = req.StartDate;
            existing.Currency = req.Currency;
            existing.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return _mapper.Map<AssetFinanceBookDto>(existing);
        }

        var book = new AssetFinanceBook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AssetId = request.AssetId,
            AcquisitionCost = req.AcquisitionCost,
            ResidualValue = req.ResidualValue,
            UsefulLifeMonths = req.UsefulLifeMonths,
            DepreciationMethod = req.DepreciationMethod,
            DepreciationRatePct = req.DepreciationRatePct,
            FrequencyMonths = req.FrequencyMonths,
            StartDate = req.StartDate,
            Currency = req.Currency,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.AssetFinanceBooks.Add(book);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return _mapper.Map<AssetFinanceBookDto>(book);
    }
}

public class DeleteFinanceBookCommandHandler : IRequestHandler<DeleteFinanceBookCommand, bool>
{
    private readonly ITenantDbContext _dbContext;
    private readonly ITenantResolver _tenantResolver;

    public DeleteFinanceBookCommandHandler(ITenantDbContext dbContext, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _tenantResolver = tenantResolver;
    }

    public async Task<bool> Handle(DeleteFinanceBookCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId().Value;

        var book = await _dbContext.AssetFinanceBooks
            .FirstOrDefaultAsync(b => b.AssetId == request.AssetId && b.TenantId == tenantId && !b.IsDeleted, cancellationToken);

        if (book == null) return false;

        var hasPostedEntries = await _dbContext.AssetDepreciationEntries
            .AnyAsync(e => e.FinanceBookId == book.Id, cancellationToken);

        if (hasPostedEntries)
            throw new DomainException("has_posted_entries", "No se puede eliminar: existen cuotas devengadas", "Domain.HasPostedEntriesDelete");

        book.IsDeleted = true;
        book.DeletedAt = DateTime.UtcNow;
        book.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}