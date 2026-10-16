using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>Batch master payload (batch-and-serial Fase 3).</summary>
public sealed record BatchDto(
    Guid Id,
    Guid ItemId,
    string BatchNo,
    DateOnly? ManufacturingDate,
    DateOnly? ExpiryDate,
    bool IsDisabled,
    string? SupplierBatch,
    string? Description)
{
    public static BatchDto From(Batch batch) =>
        new(
            batch.Id,
            batch.ItemId,
            batch.BatchId,
            batch.ManufacturingDate,
            batch.ExpiryDate,
            batch.IsDisabled,
            batch.SupplierBatch,
            batch.Description);
}

/// <summary>One FEFO availability row: the batch master with its derived on-hand quantity.</summary>
public sealed record BatchAvailabilityDto(
    Guid BatchId,
    string BatchNo,
    DateOnly? ManufacturingDate,
    DateOnly? ExpiryDate,
    bool IsDisabled,
    decimal Qty,
    bool IsExpired);

/// <summary>Serial master payload (batch-and-serial Fase 3).</summary>
public sealed record SerialNoDto(
    Guid Id,
    Guid ItemId,
    string SerialNumber,
    string Status,
    Guid? BatchId,
    string? BatchNo,
    Guid? CurrentWarehouseId,
    DateOnly? PurchaseDate,
    string? PurchaseDocumentNo,
    DateOnly? DeliveryDate,
    string? DeliveryDocumentNo)
{
    public static SerialNoDto From(SerialNo serial) =>
        new(
            serial.Id,
            serial.ItemId,
            serial.SerialNumber,
            serial.Status,
            serial.BatchId,
            serial.Batch?.BatchId,
            serial.CurrentWarehouseId,
            serial.PurchaseDate,
            serial.PurchaseDocumentNo,
            serial.DeliveryDate,
            serial.DeliveryDocumentNo);
}
