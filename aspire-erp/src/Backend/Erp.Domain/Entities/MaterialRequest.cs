using System;
using System.Collections.Generic;
using Erp.Domain.Common;

namespace Erp.Domain.Entities;

public enum MaterialRequestStatus
{
    Draft = 1,
    Submitted = 2,
    Pending = 3,
    Ordered = 4,
    Issued = 5,
    Cancelled = 6
}

public enum MaterialRequestType
{
    Purchase = 1,
    MaterialTransfer = 2,
    MaterialIssue = 3,
    Manufacture = 4
}

public class MaterialRequest : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string RequestNo { get; set; } = string.Empty;
    public DateOnly TransactionDate { get; set; }
    public DateOnly ScheduleDate { get; set; }

    public MaterialRequestType RequestType { get; set; } = MaterialRequestType.Purchase;
    public MaterialRequestStatus Status { get; set; } = MaterialRequestStatus.Draft;

    public DateTimeOffset CreatedAt { get; set; }

    public List<MaterialRequestItem> Items { get; set; } = new();
}

public class MaterialRequestItem
{
    public Guid Id { get; set; }
    public Guid MaterialRequestId { get; set; }
    public MaterialRequest? MaterialRequest { get; set; }

    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    public Guid? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public decimal Quantity { get; set; }
    public decimal OrderedQuantity { get; set; }
    public DateOnly ScheduleDate { get; set; }
}
