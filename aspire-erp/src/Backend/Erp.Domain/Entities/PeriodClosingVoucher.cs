using System;
using Erp.Domain.Common;

namespace Erp.Domain.Entities;

public enum DocumentStatus
{
    Draft,
    Submitted,
    Cancelled
}

public class PeriodClosingVoucher : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string VoucherNo { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public DateOnly PostingDate { get; set; }
    public Guid RetainedEarningsAccountId { get; set; }
    public DocumentStatus DocumentStatus { get; set; }
    public string? Remarks { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public DateTimeOffset CreatedAt { get; set; }
}
