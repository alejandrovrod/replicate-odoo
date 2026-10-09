namespace Erp.Application.DTOs;

/// <summary>
/// Stock ledger (Kardex) report payload of <c>GET /api/v1/FinancialReports/stock-ledger</c>
/// (tasks.md 7.1, ERPNext <c>stock_ledger.py</c> parity): chronological movement rows with
/// running balances per (item, warehouse), plus one opening row per pair carrying the
/// pre-period balance.
/// </summary>
/// <param name="CompanyId">Company that owns the warehouses (echoed filter).</param>
/// <param name="From">Inclusive first posting date (serialized as <c>yyyy-MM-dd</c>).</param>
/// <param name="To">Inclusive last posting date.</param>
/// <param name="Rows">Movement + opening rows, chronological within each (item, warehouse).</param>
/// <param name="TotalInQty">SUM(InQty) over movement rows (opening rows excluded).</param>
/// <param name="TotalOutQty">SUM(OutQty) over movement rows (signed, negative or zero).</param>
/// <param name="TotalValueChange">SUM(ValueChange) over movement rows.</param>
public sealed record StockLedgerReportDto(
    Guid CompanyId,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<StockLedgerRowDto> Rows,
    decimal TotalInQty,
    decimal TotalOutQty,
    decimal TotalValueChange);

/// <summary>One Kardex row: an opening balance or a single stock movement.</summary>
/// <param name="ItemId">Moved item.</param>
/// <param name="ItemCode">Display code, e.g. "IT-001".</param>
/// <param name="ItemName">Display name.</param>
/// <param name="WarehouseId">Movement warehouse.</param>
/// <param name="WarehouseCode">Display code, e.g. "WH-01".</param>
/// <param name="PostingDate">Value date of the movement (the opening row carries <c>From</c>).</param>
/// <param name="VoucherType">Source document type, e.g. "StockEntry" ("Opening" on opening rows).</param>
/// <param name="VoucherNo">Source document number.</param>
/// <param name="InQty">Receipt quantity (&gt;= 0).</param>
/// <param name="OutQty">Issue quantity (&lt;= 0).</param>
/// <param name="BalanceQty">Running on-hand after this row.</param>
/// <param name="ValuationRate">Booking rate of the movement (average rate on opening rows).</param>
/// <param name="BalanceValue">Running stock value after this row.</param>
/// <param name="ValueChange">Signed value delta of the movement (0 on opening rows).</param>
/// <param name="IsOpening">True for the synthetic pre-period balance row.</param>
public sealed record StockLedgerRowDto(
    Guid ItemId,
    string ItemCode,
    string ItemName,
    Guid WarehouseId,
    string WarehouseCode,
    DateOnly PostingDate,
    string VoucherType,
    string VoucherNo,
    decimal InQty,
    decimal OutQty,
    decimal BalanceQty,
    decimal ValuationRate,
    decimal BalanceValue,
    decimal ValueChange,
    bool IsOpening);
