using System;
using System.Collections.Generic;
using Erp.Domain.Common;

namespace Erp.Domain.Entities;

public enum SalesInvoiceStatus
{
    Draft = 1,
    Unpaid = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Cancelled = 5
}

public sealed class SalesInvoice : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    
    public string InvoiceNumber { get; set; } = null!;
    
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    
    public DateOnly PostingDate { get; set; }
    public DateOnly DueDate { get; set; }
    
    public SalesInvoiceStatus Status { get; set; } = SalesInvoiceStatus.Draft;
    
    public bool IsPOS { get; set; }
    public bool UpdateStock { get; set; }
    
    public Guid? SourceWarehouseId { get; set; }
    public Warehouse? SourceWarehouse { get; set; }
    
    public decimal NetTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }
    
    public decimal OutstandingAmount { get; set; }
    public decimal PaidAmount { get; set; }
    
    public byte[] RowVersion { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<SalesInvoiceItem> Items { get; set; } = new List<SalesInvoiceItem>();
}
