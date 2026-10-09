namespace Erp.Application.DTOs;

/// <summary>
/// Aging report payload of <c>GET /api/v1/FinancialReports/aging</c> (tasks.md 7.1, ERPNext
/// <c>accounts_receivable.py</c> parity): one row per open invoice (receivable or payable
/// leg) with Invoiced / Paid / Outstanding and the due-date bucket the outstanding falls
/// into, plus per-leg and grand totals.
/// </summary>
/// <param name="CompanyId">Company that owns the invoices (echoed filter).</param>
/// <param name="ReportDate">Aging anchor date (serialized as <c>yyyy-MM-dd</c>).</param>
/// <param name="Rows">Open-invoice rows, oldest due first within each leg.</param>
/// <param name="Totals">Aggregates per leg and combined.</param>
public sealed record AgingReportDto(
    Guid CompanyId,
    DateOnly ReportDate,
    IReadOnlyList<AgingRowDto> Rows,
    AgingTotalsDto Totals);

/// <summary>One open-invoice aging row (ERPNext columns, adapted to our invoice model).</summary>
/// <param name="PartyType">"Customer" (receivable) or "Supplier" (payable).</param>
/// <param name="PartyId">Counterparty row id.</param>
/// <param name="PartyName">Counterparty display name.</param>
/// <param name="VoucherType">"SalesInvoice" or "PurchaseInvoice".</param>
/// <param name="VoucherNo">Invoice / bill number.</param>
/// <param name="PostingDate">Invoice posting date.</param>
/// <param name="DueDate">Due date the age is measured against.</param>
/// <param name="InvoicedAmount">Invoice grand total.</param>
/// <param name="PaidAmount">GrandTotal − OutstandingAmount.</param>
/// <param name="OutstandingAmount">Open balance.</param>
/// <param name="AgeDays">ReportDate − DueDate in days (negative = not yet due).</param>
/// <param name="Bucket">Range0NotDue, Range030, Range3160, Range6190 or Range90Plus.</param>
/// <param name="Currency">Company currency fallback ("USD") - invoices carry no currency code.</param>
public sealed record AgingRowDto(
    string PartyType,
    Guid PartyId,
    string PartyName,
    string VoucherType,
    string VoucherNo,
    DateOnly PostingDate,
    DateOnly DueDate,
    decimal InvoicedAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    int AgeDays,
    string Bucket,
    string Currency);

/// <summary>Bucket totals per leg plus the combined outstanding.</summary>
/// <param name="Receivable">Totals over the Customer leg.</param>
/// <param name="Payable">Totals over the Supplier leg.</param>
public sealed record AgingTotalsDto(AgingLegTotalsDto Receivable, AgingLegTotalsDto Payable);

/// <summary>One leg of the aging totals (ERPNext range columns).</summary>
/// <param name="Outstanding">SUM(OutstandingAmount) of the leg.</param>
/// <param name="NotDue">Outstanding not yet due (AgeDays &lt; 0).</param>
/// <param name="Range030">Outstanding aged 0-30 days.</param>
/// <param name="Range3160">Outstanding aged 31-60 days.</param>
/// <param name="Range6190">Outstanding aged 61-90 days.</param>
/// <param name="Range90Plus">Outstanding aged over 90 days.</param>
public sealed record AgingLegTotalsDto(
    decimal Outstanding,
    decimal NotDue,
    decimal Range030,
    decimal Range3160,
    decimal Range6190,
    decimal Range90Plus);
