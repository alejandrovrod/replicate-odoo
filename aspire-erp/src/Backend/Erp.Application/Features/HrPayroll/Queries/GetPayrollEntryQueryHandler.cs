using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>Reads one payroll batch with every slip and its itemized lines. Read-only.</summary>
public sealed class GetPayrollEntryQueryHandler : IQueryHandler<GetPayrollEntryQuery, PayrollEntryDetailDto?>
{
    private readonly IHrPayrollRepository _hr;

    public GetPayrollEntryQueryHandler(IHrPayrollRepository hr)
    {
        _hr = hr;
    }

    public async Task<PayrollEntryDetailDto?> HandleAsync(
        GetPayrollEntryQuery query,
        CancellationToken cancellationToken = default)
    {
        var entry = await _hr.GetPayrollEntryByIdAsync(query.PayrollEntryId, cancellationToken);
        if (entry is null || entry.CompanyId != query.CompanyId)
        {
            return null;
        }

        var slips = await _hr.GetSlipsByEntryAsync(entry.Id, cancellationToken);
        return new PayrollEntryDetailDto(
            PayrollEntryDto.Build(entry, slips.Count),
            slips.Select(SalarySlipDto.Build).ToList());
    }
}
