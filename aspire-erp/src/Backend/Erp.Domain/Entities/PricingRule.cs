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

    /// <summary>
    /// Tax-engine Fase 2: which flow the template applies to ("Sales" or "Purchase").
    /// Templates are company-scoped reusable tax packages (e.g. "Honorarios PM a PF").
    /// </summary>
    public string Category { get; set; } = "Sales";

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

    /// <summary>
    /// Tax-engine Fase 2: direction of the row (Add = transferred tax, Deduct = retention).
    /// Defaults to <see cref="TaxChargeType.Add"/>, preserving the legacy flat-rate behavior.
    /// </summary>
    public TaxChargeType ChargeType { get; set; } = TaxChargeType.Add;

    /// <summary>
    /// Tax-engine Fase 2: what the rate applies on. Defaults to
    /// <see cref="TaxCalculationBasis.OnNetTotal"/> (legacy behavior). When
    /// <see cref="TaxCalculationBasis.FixedAmount"/>, <see cref="Amount"/> is the booked
    /// amount and <see cref="Rate"/> is ignored.
    /// </summary>
    public TaxCalculationBasis CalculationBasis { get; set; } = TaxCalculationBasis.OnNetTotal;
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
