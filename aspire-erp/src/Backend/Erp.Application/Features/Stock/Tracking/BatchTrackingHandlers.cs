using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Services;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Stock.Tracking;

/// <summary>FEFO-ordered availability of one item in one warehouse (picker source).</summary>
public sealed record GetBatchAvailabilityQuery(
    Guid ItemId,
    Guid WarehouseId,
    DateOnly? AsOf = null) : IQuery<IReadOnlyList<BatchAvailabilityDto>>;

/// <summary>Registers one batch master manually (receipts auto-create; this is the admin path).</summary>
public sealed record CreateBatchCommand(
    Guid ItemId,
    string BatchNo,
    DateOnly? ManufacturingDate = null,
    DateOnly? ExpiryDate = null) : ICommand<Result<BatchDto>>;

/// <summary>Retires a batch (disable) or corrects its dates (number/item immutable).</summary>
public sealed record UpdateBatchCommand(
    Guid Id,
    bool IsDisabled,
    DateOnly? ManufacturingDate = null,
    DateOnly? ExpiryDate = null) : ICommand<Result<BatchDto>>;

/// <summary>Active serials of one item in one warehouse (picker/verification source).</summary>
public sealed record GetSerialsQuery(Guid ItemId, Guid WarehouseId)
    : IQuery<IReadOnlyList<SerialNoDto>>;

/// <summary>One serial by number (or null).</summary>
public sealed record VerifySerialQuery(string SerialNumber) : IQuery<SerialNoDto?>;

public sealed class GetBatchAvailabilityQueryHandler
    : IQueryHandler<GetBatchAvailabilityQuery, IReadOnlyList<BatchAvailabilityDto>>
{
    private readonly IBatchSerialService _tracking;

    public GetBatchAvailabilityQueryHandler(IBatchSerialService tracking)
    {
        _tracking = tracking;
    }

    public async Task<IReadOnlyList<BatchAvailabilityDto>> HandleAsync(
        GetBatchAvailabilityQuery query, CancellationToken cancellationToken = default)
    {
        var rows = await _tracking.GetAvailabilityAsync(
            query.ItemId,
            query.WarehouseId,
            query.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow),
            cancellationToken);

        return rows
            .Select(r => new BatchAvailabilityDto(
                r.BatchId, r.BatchNo, r.ManufacturingDate, r.ExpiryDate,
                r.IsDisabled, r.Qty, r.IsExpired))
            .ToList();
    }
}

public sealed class CreateBatchCommandHandler : ICommandHandler<CreateBatchCommand, Result<BatchDto>>
{
    private readonly IBatchRepository _batches;
    private readonly IItemRepository _items;

    public CreateBatchCommandHandler(IBatchRepository batches, IItemRepository items)
    {
        _batches = batches;
        _items = items;
    }

    public async Task<Result<BatchDto>> HandleAsync(
        CreateBatchCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var number = (command.BatchNo ?? string.Empty).Trim();
            if (number.Length == 0 || number.Length > 100)
            {
                throw new StockValidationException(
                    StockErrorCodes.BatchRequired,
                    "Batch number is required and must be at most 100 characters.");
            }

            var item = await _items.GetByIdAsync(command.ItemId, cancellationToken)
                ?? throw new StockValidationException(
                    StockErrorCodes.ItemNotFound,
                    $"Item '{command.ItemId}' was not found in this tenant.");

            if (await _batches.GetByItemAndNumberAsync(item.Id, number, cancellationToken) is not null)
            {
                throw new StockValidationException(
                    StockErrorCodes.DuplicateBatch,
                    $"Batch '{number}' already exists for item '{item.ItemCode}'.");
            }

            var batch = new Batch
            {
                Id = Guid.NewGuid(),
                ItemId = item.Id,
                BatchId = number,
                ManufacturingDate = command.ManufacturingDate,
                ExpiryDate = command.ExpiryDate,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await _batches.AddAsync(batch, cancellationToken);
            return Result<BatchDto>.Success(BatchDto.From(batch));
        }
        catch (StockValidationException ex)
        {
            return Result<BatchDto>.Failure(ex.Code, ex.Message);
        }
    }
}

public sealed class UpdateBatchCommandHandler : ICommandHandler<UpdateBatchCommand, Result<BatchDto>>
{
    private readonly IBatchRepository _batches;

    public UpdateBatchCommandHandler(IBatchRepository batches)
    {
        _batches = batches;
    }

    public async Task<Result<BatchDto>> HandleAsync(
        UpdateBatchCommand command, CancellationToken cancellationToken = default)
    {
        var batch = await _batches.GetByIdAsync(command.Id, cancellationToken);
        if (batch is null)
        {
            return Result<BatchDto>.Failure(
                StockErrorCodes.BatchNotFound,
                $"Batch '{command.Id}' was not found in this tenant.");
        }

        batch.IsDisabled = command.IsDisabled;
        batch.ManufacturingDate = command.ManufacturingDate;
        batch.ExpiryDate = command.ExpiryDate;

        await _batches.UpdateAsync(batch, cancellationToken);
        return Result<BatchDto>.Success(BatchDto.From(batch));
    }
}

public sealed class GetSerialsQueryHandler : IQueryHandler<GetSerialsQuery, IReadOnlyList<SerialNoDto>>
{
    private readonly ISerialNoRepository _serials;

    public GetSerialsQueryHandler(ISerialNoRepository serials)
    {
        _serials = serials;
    }

    public async Task<IReadOnlyList<SerialNoDto>> HandleAsync(
        GetSerialsQuery query, CancellationToken cancellationToken = default)
    {
        var rows = await _serials.GetActiveByItemWarehouseAsync(
            query.ItemId, query.WarehouseId, cancellationToken);
        return rows.Select(SerialNoDto.From).ToList();
    }
}

public sealed class VerifySerialQueryHandler : IQueryHandler<VerifySerialQuery, SerialNoDto?>
{
    private readonly ISerialNoRepository _serials;

    public VerifySerialQueryHandler(ISerialNoRepository serials)
    {
        _serials = serials;
    }

    public async Task<SerialNoDto?> HandleAsync(
        VerifySerialQuery query, CancellationToken cancellationToken = default)
    {
        var number = (query.SerialNumber ?? string.Empty).Trim();
        if (number.Length == 0)
        {
            return null;
        }

        var rows = await _serials.GetByNumbersAsync(new[] { number }, cancellationToken);
        return rows.Count == 0 ? null : SerialNoDto.From(rows[0]);
    }
}
