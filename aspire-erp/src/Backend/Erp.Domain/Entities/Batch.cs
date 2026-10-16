using System;
using Erp.Domain.Common;

namespace Erp.Domain.Entities;

public class Batch : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string BatchId { get; set; } = string.Empty;

    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    public DateOnly? ManufacturingDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    public string? SupplierBatch { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Batch-and-serial Fase 3: disabled batches accept no movements (receipts cannot top
    /// them up, issues cannot consume them) but keep their history. Retiring replaces
    /// deleting - the Kardex derivation stays reproducible.
    /// </summary>
    public bool IsDisabled { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Lifecycle vocabulary of <see cref="SerialNo.Status"/> (batch-and-serial Fase 3):
/// plain strings, NOT an enum - <c>Status</c> is a certified <c>nvarchar</c> column whose
/// observed value is <c>"Active"</c> (and the Gherkin scenarios assert <c>"Active"</c>),
/// so constants keep the DDL untouched while giving the state machine names.
/// </summary>
public static class SerialNoStatus
{
    /// <summary>In stock at <see cref="SerialNo.CurrentWarehouseId"/>: available to dispatch.</summary>
    public const string Active = "Active";

    /// <summary>Dispatched to a customer (delivery note or material issue).</summary>
    public const string Delivered = "Delivered";

    /// <summary>Written off: terminal, never dispatchable or receivable again.</summary>
    public const string Scrapped = "Scrapped";
}

public class SerialNo : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string SerialNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";

    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    public Guid? BatchId { get; set; }
    public Batch? Batch { get; set; }

    public Guid? CurrentWarehouseId { get; set; }
    public Warehouse? CurrentWarehouse { get; set; }

    public DateOnly? PurchaseDate { get; set; }
    public string? PurchaseDocumentNo { get; set; }

    public DateOnly? DeliveryDate { get; set; }
    public string? DeliveryDocumentNo { get; set; }

    /// <summary>
    /// Batch-and-serial Fase 3: the stock-entry line that last moved this serial (receipt,
    /// issue or transfer). Lets cancellation find exactly the serials a voucher touched to
    /// revert them (delete receipt-created rows, reactivate delivered ones, move back
    /// transferred ones). Null for serials born in purchase-receipt/delivery flows, which
    /// link through <see cref="PurchaseDocumentNo"/>/<see cref="DeliveryDocumentNo"/>.
    /// </summary>
    public Guid? StockEntryItemId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
