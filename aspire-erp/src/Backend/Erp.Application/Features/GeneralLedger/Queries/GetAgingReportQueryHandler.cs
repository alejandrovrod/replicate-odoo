using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.GeneralLedger.Queries;

/// <summary>
/// Assembles <see cref="GetAgingReportQuery"/> from the two read-only aging contracts:
/// open sales invoices (receivable leg) plus open purchase bills (payable leg), bucketed
/// by due-date age. Pure read path: neither injected contract exposes writes.
/// </summary>
public sealed class GetAgingReportQueryHandler
    : IQueryHandler<GetAgingReportQuery, AgingReportDto>
{
    private readonly IReceivableAgingRepository _receivables;
    private readonly IPayableAgingRepository _payables;

    public GetAgingReportQueryHandler(
        IReceivableAgingRepository receivables,
        IPayableAgingRepository payables)
    {
        _receivables = receivables;
        _payables = payables;
    }

    public async Task<AgingReportDto> HandleAsync(
        GetAgingReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var rows = new List<AgingRowDto>();

        var receivables = await _receivables.GetOpenReceivablesByCompanyAsync(
            query.CompanyId, query.ReportDate, cancellationToken);
        foreach (var invoice in receivables)
        {
            rows.Add(MapRow(
                "Customer",
                invoice.CustomerId,
                invoice.Customer?.CustomerName ?? invoice.CustomerId.ToString(),
                "SalesInvoice",
                invoice.InvoiceNumber,
                invoice.PostingDate,
                invoice.DueDate,
                invoice.GrandTotal,
                invoice.OutstandingAmount,
                query.ReportDate));
        }

        var payables = await _payables.GetOpenPayablesByCompanyAsync(
            query.CompanyId, query.ReportDate, cancellationToken);
        foreach (var bill in payables)
        {
            rows.Add(MapRow(
                "Supplier",
                bill.SupplierId,
                bill.Supplier?.Name ?? bill.SupplierId.ToString(),
                "PurchaseInvoice",
                bill.BillNumber,
                bill.PostingDate,
                bill.DueDate,
                bill.GrandTotal,
                bill.OutstandingAmount,
                query.ReportDate));
        }

        var receivableTotals = TotalLeg(rows.Where(r => r.PartyType == "Customer"));
        var payableTotals = TotalLeg(rows.Where(r => r.PartyType == "Supplier"));

        return new AgingReportDto(
            query.CompanyId, query.ReportDate, rows, new AgingTotalsDto(receivableTotals, payableTotals));
    }

    private static AgingRowDto MapRow(
        string partyType,
        Guid partyId,
        string partyName,
        string voucherType,
        string voucherNo,
        DateOnly postingDate,
        DateOnly dueDate,
        decimal grandTotal,
        decimal outstanding,
        DateOnly reportDate)
    {
        var ageDays = reportDate.DayNumber - dueDate.DayNumber;
        return new AgingRowDto(
            partyType,
            partyId,
            partyName,
            voucherType,
            voucherNo,
            postingDate,
            dueDate,
            grandTotal,
            grandTotal - outstanding,
            outstanding,
            ageDays,
            BucketFor(ageDays),
            "USD");
    }

    /// <summary>
    /// ERPNext bucket assignment (accounts_receivable.py ranges, adapted to the pinned
    /// 0-30 / 31-60 / 61-90 / 90+ grid plus a NotDue bucket for negative ages).
    /// </summary>
    public static string BucketFor(int ageDays) =>
        ageDays < 0 ? "Range0NotDue"
        : ageDays <= 30 ? "Range030"
        : ageDays <= 60 ? "Range3160"
        : ageDays <= 90 ? "Range6190"
        : "Range90Plus";

    private static AgingLegTotalsDto TotalLeg(IEnumerable<AgingRowDto> leg)
    {
        decimal outstanding = 0m;
        decimal notDue = 0m;
        decimal r030 = 0m;
        decimal r3160 = 0m;
        decimal r6190 = 0m;
        decimal r90Plus = 0m;

        foreach (var row in leg)
        {
            outstanding += row.OutstandingAmount;
            switch (row.Bucket)
            {
                case "Range0NotDue": notDue += row.OutstandingAmount; break;
                case "Range030": r030 += row.OutstandingAmount; break;
                case "Range3160": r3160 += row.OutstandingAmount; break;
                case "Range6190": r6190 += row.OutstandingAmount; break;
                default: r90Plus += row.OutstandingAmount; break;
            }
        }

        return new AgingLegTotalsDto(outstanding, notDue, r030, r3160, r6190, r90Plus);
    }
}
