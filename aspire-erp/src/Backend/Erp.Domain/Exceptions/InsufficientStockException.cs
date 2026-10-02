using Erp.Domain.Entities;

namespace Erp.Domain.Exceptions;

/// <summary>
/// Task 3.3 acceptance criterion: issuing (or transferring) more stock than the warehouse holds
/// throws this exception when the company policy forbids negative inventory
/// (<c>Company.AllowNegativeStock = 0</c>). The message always carries the item code, the
/// warehouse, the available quantity and the requested quantity so the operator can act on it.
/// </summary>
public sealed class InsufficientStockException : Exception
{
    /// <summary>Stable failure code carried into the RFC 7807 <c>code</c> extension.</summary>
    public string Code { get; } = StockErrorCodes.InsufficientStock;

    public string ItemCode { get; }

    public string WarehouseCode { get; }

    public decimal Available { get; }

    public decimal Requested { get; }

    public InsufficientStockException(string itemCode, string warehouseCode, decimal available, decimal requested)
        : base(
            $"Insufficient stock for item '{itemCode}' in warehouse '{warehouseCode}': "
            + $"available {available:0.####}, requested {requested:0.####}. "
            + "Enable AllowNegativeStock on the company to post this movement.")
    {
        ItemCode = itemCode;
        WarehouseCode = warehouseCode;
        Available = available;
        Requested = requested;
    }
}
