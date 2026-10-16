using System;
using System.Collections.Generic;
using Erp.Domain.Common;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

public enum SalesInvoiceStatus
{
    Draft = 1,
    Unpaid = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Cancelled = 5
}

public sealed class SalesInvoice : ITenantEntity, ICustomFieldsCarrier, IWorkflowCarrier
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

    /// <summary>
    /// ERPNext parity (advances allocation): total of the <see cref="PaymentAllocation"/>
    /// slices crossed against this invoice (decimal(18,4), never negative, never above
    /// <see cref="GrandTotal"/>). Set at creation from the validated advance inputs and
    /// settled at submit; <c>OutstandingAmount = GrandTotal - TotalAdvance</c>.
    /// </summary>
    public decimal TotalAdvance { get; set; }

    /// <summary>
    /// Advance-cross slices against this invoice (the invoice side of the
    /// <c>PaymentAllocation</c> aggregate): created with the Draft, settled at submit by
    /// consuming the referenced payment's <c>UnallocatedAmount</c>.
    /// </summary>
    public ICollection<PaymentAllocation> Advances { get; set; } = new List<PaymentAllocation>();
    
    public byte[] RowVersion { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Tenant-configured dynamic attributes, see <see cref="ICustomFieldsCarrier"/>.</summary>
    public string? CustomData { get; set; }

    /// <summary>
    /// Operational workflow state (workflow Fase 5, <see cref="IWorkflowCarrier"/>): see
    /// <see cref="SalesOrder.WorkflowState"/> - same contract, SalesInvoice flow.
    /// </summary>
    public string? WorkflowState { get; set; }

    public ICollection<SalesInvoiceItem> Items { get; set; } = new List<SalesInvoiceItem>();

    /// <summary>"Taxes and Charges" breakdown rows (module 17). Empty for tax-free invoices.</summary>
    public ICollection<SalesInvoiceTax> Taxes { get; set; } = new List<SalesInvoiceTax>();

    /// <summary>
    /// Crosses one advance slice against this Draft invoice (ERPNext parity
    /// <c>Sales Invoice Advance</c>): appends the slice and keeps
    /// <c>TotalAdvance / OutstandingAmount</c> consistent.
    /// </summary>
    /// <exception cref="InvalidOperationException">The invoice is no longer a Draft.</exception>
    /// <exception cref="SalesValidationException">
    /// <c>advance_exceeds_invoice_total</c> when the running total would pass
    /// <see cref="GrandTotal"/> (spec scenario SI-G3).
    /// </exception>
    public void AllocateAdvance(Guid paymentEntryId, decimal allocatedAmount)
    {
        if (Status != SalesInvoiceStatus.Draft)
        {
            throw new InvalidOperationException("Advances can only be allocated on draft invoices.");
        }

        if (TotalAdvance + allocatedAmount > GrandTotal)
        {
            throw new SalesValidationException(
                SellingErrorCodes.AdvanceExceedsInvoiceTotal,
                $"Allocated advance ({TotalAdvance + allocatedAmount:0.####}) cannot exceed "
                + $"the invoice total ({GrandTotal:0.####}).");
        }

        Advances.Add(new PaymentAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            SalesInvoiceId = Id,
            SalesInvoice = this,
            PaymentEntryId = paymentEntryId,
            AllocatedAmount = allocatedAmount,
            ReferenceDocumentType = "SalesInvoice",
            ReferenceDocumentId = Id,
            TotalAmount = GrandTotal,
            OutstandingAmount = GrandTotal,
            ExchangeRate = ExchangeRate,
        });
        TotalAdvance += allocatedAmount;
        OutstandingAmount = GrandTotal - TotalAdvance;
    }
}
