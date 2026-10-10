using System;
using System.Collections.Generic;
using Erp.Domain.Common;

namespace Erp.Domain.Entities;

public enum QuotationStatus
{
    Draft = 1,
    Submitted = 2,
    Lost = 3,
    Ordered = 4,
    Cancelled = 5
}

public class Quotation : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string QuotationNo { get; set; } = string.Empty;
    public DateOnly TransactionDate { get; set; }
    public DateOnly ValidTill { get; set; }

    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public QuotationStatus Status { get; set; } = QuotationStatus.Draft;

    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal ExchangeRate { get; set; } = 1.0m;

    public string? TermsAndConditions { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<QuotationItem> Items { get; set; } = new();
}

public class QuotationItem
{
    public Guid Id { get; set; }
    public Guid QuotationId { get; set; }
    public Quotation? Quotation { get; set; }

    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount => Quantity * Rate;
}
