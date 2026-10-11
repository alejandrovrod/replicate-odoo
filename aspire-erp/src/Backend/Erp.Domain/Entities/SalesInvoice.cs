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
    
    public Guid? CurrencyId { get; set; }
    public Currency? Currency { get; set; }
    public decimal ExchangeRate { get; set; } = 1m;

    
    public DateOnly PostingDate { get; set; }
    public DateOnly DueDate { get; set; }
    
    public SalesInvoiceStatus Status { get; set; } = SalesInvoiceStatus.Draft;
    
    public bool IsPOS { get; set; }
    public bool UpdateStock { get; set; }

    /// <summary>
    /// Credit-note mode, module 18 (ERPNext "Is Return"): a submitted return carries
    /// all-negative lines and totals and books inverse GL sides on submit.
    /// </summary>
    public bool IsReturn { get; set; }

    /// <summary>Original submitted invoice being credited (required when <see cref="IsReturn"/>).</summary>
    public Guid? ReturnAgainstId { get; set; }
    public SalesInvoice? ReturnAgainst { get; set; }
    
    public Guid? SourceWarehouseId { get; set; }
    public Warehouse? SourceWarehouse { get; set; }
    
    public decimal NetTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }

    /// <summary>
    /// Global discount, module 17 (ERPNext default "apply on Net Total"): percentage (0-100)
    /// applied to <see cref="NetTotal"/> before taxes are computed.
    /// </summary>
    public decimal DiscountPercentage { get; set; }

    /// <summary>
    /// Global discount amount, always stored non-negative: <c>GrandTotal = NetTotal - DiscountAmount + TaxTotal</c>.
    /// </summary>
    public decimal DiscountAmount { get; set; }
        
    public decimal OutstandingAmount { get; set; }
    public decimal PaidAmount { get; set; }
    
    public byte[] RowVersion { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<SalesInvoiceItem> Items { get; set; } = new List<SalesInvoiceItem>();

    /// <summary>"Taxes and Charges" breakdown rows (module 17). Empty for tax-free invoices.</summary>
    public ICollection<SalesInvoiceTax> Taxes { get; set; } = new List<SalesInvoiceTax>();
}
