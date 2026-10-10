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
    public DateTimeOffset CreatedAt { get; set; }
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

    public DateTimeOffset CreatedAt { get; set; }
}
