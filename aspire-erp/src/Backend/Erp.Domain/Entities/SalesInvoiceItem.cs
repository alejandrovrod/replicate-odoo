using System;

namespace Erp.Domain.Entities;

public sealed class SalesInvoiceItem
{
    public Guid Id { get; set; }
    
    public Guid SalesInvoiceId { get; set; }
    public SalesInvoice? SalesInvoice { get; set; }
    
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }
    
    public Guid? SalesOrderItemId { get; set; }
    
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
}
