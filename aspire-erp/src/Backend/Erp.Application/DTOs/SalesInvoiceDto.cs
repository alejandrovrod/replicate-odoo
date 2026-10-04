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
    IReadOnlyList<SalesInvoiceItemDto> Items);

public sealed record SalesInvoiceItemDto(
    Guid Id,
    Guid ItemId,
    Guid? SalesOrderItemId,
    decimal Quantity,
    decimal Rate,
    decimal Amount);
