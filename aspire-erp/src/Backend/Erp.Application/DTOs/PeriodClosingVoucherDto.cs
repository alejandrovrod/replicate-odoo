using System;
using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

public record PeriodClosingVoucherDto(
    Guid Id,
    Guid CompanyId,
    string VoucherNo,
    DateOnly PostingDate,
    Guid RetainedEarningsAccountId,
    DocumentStatus DocumentStatus,
    string? Remarks
);
