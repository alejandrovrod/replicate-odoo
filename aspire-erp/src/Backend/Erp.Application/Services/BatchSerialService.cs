using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Services;

/// <inheritdoc cref="IBatchSerialService"/>
public sealed class BatchSerialService : IBatchSerialService
{
    private readonly IBatchRepository _batches;
    private readonly ISerialNoRepository _serials;
    private readonly IStockRepository _stock;

    public BatchSerialService(
        IBatchRepository batches,
        ISerialNoRepository serials,
        IStockRepository stock)
    {
        _batches = batches;
        _serials = serials;
        _stock = stock;
    }

    public async Task<Batch> EnsureBatchForReceiptAsync(
        Guid itemId,
        string itemCode,
        string batchNo,
        DateOnly? manufacturingDate,
        DateOnly? expiryDate,
        CancellationToken cancellationToken = default)
    {
        var number = (batchNo ?? string.Empty).Trim();
        if (number.Length == 0)
        {
            throw new StockValidationException(
                StockErrorCodes.BatchRequired,
                $"Item '{itemCode}' is batch-tracked: a receipt line must name a batch number.");
        }

        var existing = await _batches.GetByItemAndNumberAsync(itemId, number, cancellationToken);
        if (existing is not null)
        {
            if (existing.IsDisabled)
            {
                throw new StockValidationException(
                    StockErrorCodes.BatchDisabled,
                    $"Batch '{number}' of item '{itemCode}' is disabled and accepts no receipts.");
            }

            return existing;
        }

        var batch = new Batch
        {
            Id = Guid.NewGuid(),
            ItemId = itemId,
            BatchId = number,
            ManufacturingDate = manufacturingDate,
            ExpiryDate = expiryDate,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await _batches.AddAsync(batch, cancellationToken);
        return batch;
    }

    public async Task<Batch> EnsureBatchForIssueAsync(
        Guid itemId,
        string itemCode,
        Guid batchId,
        Guid warehouseId,
        string warehouseCode,
        decimal qty,
        DateOnly postingDate,
        bool allowNegativeStock,
        IReadOnlyDictionary<Guid, decimal>? consumedByBatch = null,
        CancellationToken cancellationToken = default)
    {
        var batch = await _batches.GetByIdAsync(batchId, cancellationToken)
            ?? throw new StockValidationException(
                StockErrorCodes.BatchNotFound,
                $"Batch '{batchId}' was not found in this tenant.");

        if (batch.ItemId != itemId)
        {
            throw new StockValidationException(
                StockErrorCodes.BatchNotFound,
                $"Batch '{batch.BatchId}' does not belong to item '{itemCode}'.");
        }

        if (batch.IsDisabled)
        {
            throw new StockValidationException(
                StockErrorCodes.BatchDisabled,
                $"Batch '{batch.BatchId}' of item '{itemCode}' is disabled and cannot be dispatched.");
        }

        if (batch.ExpiryDate.HasValue && batch.ExpiryDate.Value < postingDate)
        {
            throw new StockValidationException(
                StockErrorCodes.BatchHasExpired,
                $"Batch '{batch.BatchId}' of item '{itemCode}' expired on {batch.ExpiryDate:yyyy-MM-dd} "
                + "and cannot be dispatched.");
        }

        if (!allowNegativeStock)
        {
            var balances = await _stock.GetBatchBalancesAsync(itemId, warehouseId, postingDate, cancellationToken);
            var onHand = balances.FirstOrDefault(b => b.BatchId == batch.Id)?.Qty ?? 0m;
            var alreadyConsumed = consumedByBatch is not null && consumedByBatch.TryGetValue(batch.Id, out var used)
                ? used
                : 0m;

            if (qty > onHand - alreadyConsumed)
            {
                throw new StockValidationException(
                    StockErrorCodes.BatchInsufficientStock,
                    $"Batch '{batch.BatchId}' holds {onHand - alreadyConsumed:0.####} units in warehouse "
                    + $"'{warehouseCode}' but the line dispatches {qty:0.####}.");
            }
        }

        return batch;
    }

    public async Task<IReadOnlyList<BatchAvailability>> GetAvailabilityAsync(
        Guid itemId,
        Guid warehouseId,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        var masters = await _batches.GetByItemAsync(itemId, cancellationToken);
        if (masters.Count == 0)
        {
            return Array.Empty<BatchAvailability>();
        }

        var balances = await _stock.GetBatchBalancesAsync(itemId, warehouseId, asOf, cancellationToken);
        var qtyByBatch = balances.ToDictionary(b => b.BatchId, b => b.Qty);

        return masters
            .Where(b => qtyByBatch.TryGetValue(b.Id, out var qty) && qty != 0m)
            .Select(b => new BatchAvailability(
                b.Id,
                b.BatchId,
                b.ManufacturingDate,
                b.ExpiryDate,
                b.IsDisabled,
                qtyByBatch[b.Id],
                b.ExpiryDate.HasValue && b.ExpiryDate.Value < asOf))
            .OrderBy(b => b.ExpiryDate.HasValue ? 0 : 1)
            .ThenBy(b => b.ExpiryDate)
            .ThenBy(b => b.BatchNo, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<SerialNo>> StageReceiptSerialsAsync(
        Guid itemId,
        string itemCode,
        IReadOnlyList<string> serials,
        Guid warehouseId,
        Guid? batchId,
        DateOnly postingDate,
        CancellationToken cancellationToken = default)
    {
        var numbers = CleanSerials(serials);
        if (numbers.Count == 0)
        {
            return Array.Empty<SerialNo>();
        }

        var existing = await _serials.GetByNumbersAsync(numbers, cancellationToken);
        if (existing.Count > 0)
        {
            throw new StockValidationException(
                StockErrorCodes.SerialAlreadyExists,
                $"Serial '{existing[0].SerialNumber}' already exists in this tenant: serial numbers are never reused.");
        }

        return numbers.Select(number => new SerialNo
        {
            Id = Guid.NewGuid(),
            ItemId = itemId,
            SerialNumber = number,
            Status = SerialNoStatus.Active,
            BatchId = batchId,
            CurrentWarehouseId = warehouseId,
            PurchaseDate = postingDate,
            CreatedAt = DateTimeOffset.UtcNow,
        }).ToList();
    }

    public async Task<IReadOnlyList<SerialNo>> StageIssueSerialsAsync(
        Guid itemId,
        string itemCode,
        IReadOnlyList<string> serials,
        Guid warehouseId,
        string warehouseCode,
        DateOnly postingDate,
        CancellationToken cancellationToken = default)
    {
        var numbers = CleanSerials(serials);
        if (numbers.Count == 0)
        {
            return Array.Empty<SerialNo>();
        }

        var found = (await _serials.GetByNumbersAsync(numbers, cancellationToken))
            .ToDictionary(s => s.SerialNumber, StringComparer.Ordinal);

        var staged = new List<SerialNo>(numbers.Count);
        foreach (var number in numbers)
        {
            // Spec STK-TRK-G3: anything not dispatchable from this warehouse - unknown,
            // retired, or sitting elsewhere - reports "not in warehouse" (the actionable
            // fact for the operator; existence is an internal detail).
            if (!found.TryGetValue(number, out var serial)
                || serial.ItemId != itemId
                || serial.Status != SerialNoStatus.Active
                || serial.CurrentWarehouseId != warehouseId)
            {
                throw new StockValidationException(
                    StockErrorCodes.SerialNotInWarehouse,
                    $"Serial '{number}' of item '{itemCode}' is not available in warehouse '{warehouseCode}'.");
            }

            serial.Status = SerialNoStatus.Delivered;
            serial.DeliveryDate = postingDate;
            staged.Add(serial);
        }

        return staged;
    }

    public async Task<IReadOnlyList<SerialNo>> StageTransferSerialsAsync(
        Guid itemId,
        string itemCode,
        IReadOnlyList<string> serials,
        Guid sourceWarehouseId,
        string sourceWarehouseCode,
        Guid targetWarehouseId,
        CancellationToken cancellationToken = default)
    {
        var numbers = CleanSerials(serials);
        if (numbers.Count == 0)
        {
            return Array.Empty<SerialNo>();
        }

        var found = (await _serials.GetByNumbersAsync(numbers, cancellationToken))
            .ToDictionary(s => s.SerialNumber, StringComparer.Ordinal);

        var staged = new List<SerialNo>(numbers.Count);
        foreach (var number in numbers)
        {
            if (!found.TryGetValue(number, out var serial)
                || serial.ItemId != itemId
                || serial.Status != SerialNoStatus.Active
                || serial.CurrentWarehouseId != sourceWarehouseId)
            {
                throw new StockValidationException(
                    StockErrorCodes.SerialNotInWarehouse,
                    $"Serial '{number}' of item '{itemCode}' is not available in warehouse '{sourceWarehouseCode}'.");
            }

            serial.CurrentWarehouseId = targetWarehouseId;
            staged.Add(serial);
        }

        return staged;
    }

    public Task PersistSerialsAsync(
        IReadOnlyList<SerialNo> toAdd,
        IReadOnlyList<SerialNo> toUpdate,
        CancellationToken cancellationToken = default)
        => Task.WhenAll(
            toAdd.Count == 0
                ? Task.CompletedTask
                : _serials.AddRangeAsync(toAdd, cancellationToken),
            toUpdate.Count == 0
                ? Task.CompletedTask
                : _serials.UpdateRangeAsync(toUpdate, cancellationToken));

    public Task DeleteSerialsAsync(
        IReadOnlyList<SerialNo> serials,
        CancellationToken cancellationToken = default) =>
        serials.Count == 0
            ? Task.CompletedTask
            : _serials.DeleteRangeAsync(serials, cancellationToken);

    private static IReadOnlyList<string> CleanSerials(IReadOnlyList<string> serials) =>
        serials
            .Select(s => (s ?? string.Empty).Trim())
            .Where(s => s.Length > 0)
            .ToList();
}
