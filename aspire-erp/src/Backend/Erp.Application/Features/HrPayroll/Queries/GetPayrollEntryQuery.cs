using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>Reads one payroll batch with every slip and its itemized lines. Read-only.</summary>
public sealed record GetPayrollEntryQuery(Guid CompanyId, Guid PayrollEntryId) : IQuery<PayrollEntryDetailDto?>;
