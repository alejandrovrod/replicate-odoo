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

public class CreateDisposalCommandHandler : IRequestHandler<CreateDisposalCommand, AssetDisposalDto>
{
    private readonly ITenantDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly ITenantResolver _tenantResolver;

    public CreateDisposalCommandHandler(ITenantDbContext dbContext, IMapper mapper, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _tenantResolver = tenantResolver;
    }

    public async Task<AssetDisposalDto> Handle(CreateDisposalCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId().Value;

        var asset = await _dbContext.Assets
            .FirstOrDefaultAsync(a => a.Id == request.AssetId && a.TenantId == tenantId && !a.IsDeleted, cancellationToken);
        if (asset == null) throw new DomainException("asset_not_found", "Activo no encontrado");

        var existingDisposal = await _dbContext.AssetDisposals
            .FirstOrDefaultAsync(d => d.AssetId == request.AssetId, cancellationToken);
        if (existingDisposal != null)
            throw new DomainException("already_disposed", "El activo ya tiene baja registrada");

        var req = request.Request;

        if (req.ProceedsAmount < 0)
            throw new DomainException("negative_proceeds", "El monto de recuperación no puede ser negativo");

        var lastPostedEntry = await _dbContext.AssetDepreciationEntries
            .Where(e => e.AssetId == request.AssetId)
            .OrderByDescending(e => e.PeriodNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var book = await _dbContext.AssetFinanceBooks
            .FirstOrDefaultAsync(b => b.AssetId == request.AssetId && b.TenantId == tenantId && !b.IsDeleted, cancellationToken);

        var netBookValueAtDisposal = lastPostedEntry?.NetBookValue ?? book?.AcquisitionCost ?? 0;
        var gainLossAmount = req.ProceedsAmount - netBookValueAtDisposal;

        var disposal = new AssetDisposal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AssetId = request.AssetId,
            DisposalType = req.DisposalType,
            DisposalDate = req.DisposalDate,
            NetBookValueAtDisposal = netBookValueAtDisposal,
            ProceedsAmount = req.ProceedsAmount,
            GainLossAmount = gainLossAmount,
            Reason = req.Reason,
            DocumentReference = req.DocumentReference,
            ApprovedBy = Guid.Empty, // TODO: contexto auth
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AssetDisposals.Add(disposal);

        asset.State = req.DisposalType == DisposalType.Scrapped ? "Scrapped" : "Disposed";
        asset.UpdatedAt = DateTime.UtcNow;

        var futureSchedules = await _dbContext.AssetDepreciationSchedules
            .Where(s => s.AssetId == request.AssetId && !s.IsPosted && !s.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var s in futureSchedules)
        {
            s.IsDeleted = true;
            s.DeletedAt = DateTime.UtcNow;
            s.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return _mapper.Map<AssetDisposalDto>(disposal);
    }
}

public class CreateCustodyTransferCommandHandler : IRequestHandler<CreateCustodyTransferCommand, AssetCustodyTransferDto>
{
    private readonly ITenantDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly ITenantResolver _tenantResolver;

    public CreateCustodyTransferCommandHandler(ITenantDbContext dbContext, IMapper mapper, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _tenantResolver = tenantResolver;
    }

    public async Task<AssetCustodyTransferDto> Handle(CreateCustodyTransferCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId().Value;

        var asset = await _dbContext.Assets
            .FirstOrDefaultAsync(a => a.Id == request.AssetId && a.TenantId == tenantId && !a.IsDeleted, cancellationToken);
        if (asset == null) throw new DomainException("asset_not_found", "Activo no encontrado");

        if (asset.State is "Disposed" or "Scrapped")
            throw new DomainException("asset_disposed", "No se pueden registrar transferencias en activo dado de baja");

        var req = request.Request;

        var toEmployee = await _dbContext.Employees
            .FirstOrDefaultAsync(e => e.Id == req.ToEmployeeId && e.TenantId == tenantId && !e.IsDeleted, cancellationToken);
        if (toEmployee == null)
            throw new DomainException("employee_not_found", "Empleado destinatario no encontrado");

        Guid? fromEmployeeId = null;
        if (req.TransferType != CustodyTransferType.Assignment)
        {
            var lastTransfer = await _dbContext.AssetCustodyTransfers
                .Where(t => t.AssetId == request.AssetId && !t.IsDeleted)
                .OrderByDescending(t => t.TransferDate)
                .FirstOrDefaultAsync(cancellationToken);

            fromEmployeeId = lastTransfer?.ToEmployeeId;
        }

        if (req.TransferType == CustodyTransferType.Assignment && fromEmployeeId.HasValue)
            throw new DomainException("already_assigned", "El activo ya tiene custodio asignado. Use TransferType=Transfer");

        var transfer = new AssetCustodyTransfer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AssetId = request.AssetId,
            FromEmployeeId = fromEmployeeId,
            ToEmployeeId = req.ToEmployeeId,
            FromDepartmentId = req.FromDepartmentId,
            ToDepartmentId = req.ToDepartmentId,
            TransferDate = req.TransferDate,
            TransferType = req.TransferType,
            Reason = req.Reason,
            DocumentUrl = req.DocumentUrl,
            SignedByFrom = req.SignedByFrom,
            SignedByTo = req.SignedByTo,
            CreatedBy = Guid.Empty, // TODO: contexto auth
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AssetCustodyTransfers.Add(transfer);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return _mapper.Map<AssetCustodyTransferDto>(transfer);
    }
}

public class CapitalizeMaintenanceOrderCommandHandler : IRequestHandler<CapitalizeMaintenanceOrderCommand, AssetRepairCapitalizationDto>
{
    private readonly ITenantDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly ITenantResolver _tenantResolver;

    public CapitalizeMaintenanceOrderCommandHandler(ITenantDbContext dbContext, IMapper mapper, ITenantResolver tenantResolver)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _tenantResolver = tenantResolver;
    }

    public async Task<AssetRepairCapitalizationDto> Handle(CapitalizeMaintenanceOrderCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId().Value;

        var order = await _dbContext.MaintenanceOrders
            .Include(o => o.Parts)
            .FirstOrDefaultAsync(o => o.Id == request.MaintenanceOrderId && o.TenantId == tenantId && !o.IsDeleted, cancellationToken);
        if (order == null) throw new DomainException("maintenance_order_not_found", "Orden de mantenimiento no encontrada");

        if (order.State is not "Completed" and not "Verified")
            throw new DomainException("order_not_completed", "Solo órdenes Completed o Verified pueden capitalizarse");

        var existingCapitalization = await _dbContext.AssetRepairCapitalizations
            .FirstOrDefaultAsync(c => c.MaintenanceOrderId == request.MaintenanceOrderId && !c.IsDeleted, cancellationToken);
        if (existingCapitalization != null)
            throw new DomainException("already_capitalized", "La orden ya ha sido capitalizada");

        var asset = await _dbContext.Assets
            .FirstOrDefaultAsync(a => a.Id == order.AssetId && a.TenantId == tenantId && !a.IsDeleted, cancellationToken);
        if (asset == null) throw new DomainException("asset_not_found", "Activo asociado no encontrado");

        if (asset.State is "Disposed" or "Scrapped")
            throw new DomainException("asset_disposed", "No se puede capitalizar en activo dado de baja");

        var book = await _dbContext.AssetFinanceBooks
            .FirstOrDefaultAsync(b => b.AssetId == order.AssetId && b.TenantId == tenantId && !b.IsDeleted, cancellationToken);
        if (book == null)
            throw new DomainException("finance_book_not_found", "El activo no tiene perfil financiero configurado");

        var req = request.Request;

        var partsCost = order.Parts.Sum(p => p.Quantity * p.UnitCost);
        var capitalizedAmount = order.LaborCost + partsCost;

        if (capitalizedAmount <= 0)
            throw new DomainException("zero_capitalization", "El costo total a capitalizar debe ser mayor a cero");

        var oldAcquisitionCost = book.AcquisitionCost;
        book.AcquisitionCost += capitalizedAmount;

        if (req.NewUsefulLifeMonths.HasValue)
        {
            if (req.NewUsefulLifeMonths <= 0)
                throw new DomainException("invalid_new_life", "La nueva vida útil debe ser mayor a 0");
            book.UsefulLifeMonths = req.NewUsefulLifeMonths.Value;
        }

        book.UpdatedAt = DateTime.UtcNow;

        var capitalization = new AssetRepairCapitalization
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MaintenanceOrderId = request.MaintenanceOrderId,
            AssetId = order.AssetId,
            CapitalizedAmount = capitalizedAmount,
            NewUsefulLifeMonths = req.NewUsefulLifeMonths,
            EffectiveDate = req.EffectiveDate,
            ApprovedBy = Guid.Empty, // TODO: contexto auth
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AssetRepairCapitalizations.Add(capitalization);

        await RecalculateFutureSchedules(book, req.EffectiveDate, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return _mapper.Map<AssetRepairCapitalizationDto>(capitalization);
    }

    private async Task RecalculateFutureSchedules(AssetFinanceBook book, DateTime effectiveDate, CancellationToken cancellationToken)
    {
        var futureSchedules = await _dbContext.AssetDepreciationSchedules
            .Where(s => s.AssetId == book.AssetId && !s.IsPosted && !s.IsDeleted && s.PeriodStartDate >= effectiveDate)
            .OrderBy(s => s.PeriodNumber)
            .ToListAsync(cancellationToken);

        if (!futureSchedules.Any()) return;

        var lastPostedEntry = await _dbContext.AssetDepreciationEntries
            .Where(e => e.AssetId == book.AssetId)
            .OrderByDescending(e => e.PeriodNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var currentNetBookValue = lastPostedEntry?.NetBookValue ?? book.AcquisitionCost;
        var depreciableRemaining = currentNetBookValue - book.ResidualValue;
        var remainingPeriods = futureSchedules.Count;
        decimal accumulated = book.AcquisitionCost - currentNetBookValue;

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
                    return;
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