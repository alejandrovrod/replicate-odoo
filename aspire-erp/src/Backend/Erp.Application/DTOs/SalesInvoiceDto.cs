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
    IReadOnlyList<SalesInvoiceItemDto> Items)
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
            invoice.Items.Select(i => new SalesInvoiceItemDto(i.Id, i.ItemId, i.SalesOrderItemId, i.Quantity, i.Rate, i.Amount)).ToList());
    }
}

public sealed record SalesInvoiceItemDto(
    Guid Id,
    Guid ItemId,
    Guid? SalesOrderItemId,
    decimal Quantity,
    decimal Rate,
    decimal Amount);
