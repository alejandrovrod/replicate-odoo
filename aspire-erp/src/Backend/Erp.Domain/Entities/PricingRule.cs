using System;
using System.Collections.Generic;
using Erp.Domain.Common;

namespace Erp.Domain.Entities;

public class TaxTemplate : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Title { get; set; } = string.Empty;
    public bool IsDefault { get; set; }

    public List<TaxTemplateItem> Taxes { get; set; } = new();
}

public class TaxTemplateItem
{
    public Guid Id { get; set; }
    public Guid TaxTemplateId { get; set; }
    public TaxTemplate? TaxTemplate { get; set; }

    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public string Description { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
}

public class PricingRule : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string RuleName { get; set; } = string.Empty;
    
    public Guid? ItemId { get; set; }
    public Item? Item { get; set; }

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public decimal MinQuantity { get; set; }
    public decimal MaxQuantity { get; set; }

    public decimal PriceOrDiscount { get; set; }
    public string MarginType { get; set; } = "Percentage"; // "Percentage" or "Amount"

    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidUpto { get; set; }

    public bool Disable { get; set; }
}
