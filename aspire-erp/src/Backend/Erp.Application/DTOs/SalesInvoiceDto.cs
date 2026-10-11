using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

public sealed record SalesInvoiceDto(
    Guid Id,
    Guid CompanyId,
    string InvoiceNumber,
    Guid CustomerId,
    string? CustomerName,
    DateOnly PostingDate,
    DateOnly DueDate,
    SalesInvoiceStatus Status,
    bool IsPOS,
    bool UpdateStock,
    Guid? SourceWarehouseId,
    decimal NetTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    decimal OutstandingAmount,
    decimal PaidAmount,
    string RowVersion,
    DateTimeOffset CreatedAt,
    IReadOnlyList<SalesInvoiceItemDto> Items,
    // Module 17 (taxes & global discount) + module 18 (returns): defaulted so the
    // pre-existing positional constructions keep compiling; Build() always populates them.
    decimal DiscountPercentage = 0m,
    decimal DiscountAmount = 0m,
    IReadOnlyList<SalesInvoiceTaxDto>? Taxes = null,
    bool IsReturn = false,
    Guid? ReturnAgainstId = null)
{
    public static SalesInvoiceDto Build(SalesInvoice invoice)
    {
        return new SalesInvoiceDto(
            invoice.Id,
            invoice.CompanyId,
            invoice.InvoiceNumber,
            invoice.CustomerId,
            invoice.Customer?.CustomerName,
            invoice.PostingDate,
            invoice.DueDate,
            invoice.Status,
            invoice.IsPOS,
            invoice.UpdateStock,
            invoice.SourceWarehouseId,
            invoice.NetTotal,
            invoice.TaxTotal,
            invoice.GrandTotal,
            invoice.OutstandingAmount,
            invoice.PaidAmount,
            Convert.ToBase64String(invoice.RowVersion ?? Array.Empty<byte>()),
            invoice.CreatedAt,
            invoice.Items.Select(i => new SalesInvoiceItemDto(i.Id, i.ItemId, i.SalesOrderItemId, i.Quantity, i.Rate, i.Amount)).ToList(),
            invoice.DiscountPercentage,
            invoice.DiscountAmount,
            invoice.Taxes.Select(t => new SalesInvoiceTaxDto(t.Id, t.AccountId, t.Rate, t.TaxAmount)).ToList(),
            invoice.IsReturn,
            invoice.ReturnAgainstId);
    }
}

public sealed record SalesInvoiceItemDto(
    Guid Id,
    Guid ItemId,
    Guid? SalesOrderItemId,
    decimal Quantity,
    decimal Rate,
    decimal Amount);

/// <summary>One "Taxes and Charges" row as stored (module 17).</summary>
public sealed record SalesInvoiceTaxDto(
    Guid Id,
    Guid AccountId,
    decimal Rate,
    decimal TaxAmount);
