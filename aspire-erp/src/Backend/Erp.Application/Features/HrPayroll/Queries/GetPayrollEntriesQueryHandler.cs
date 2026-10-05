using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>Lists one company's payroll batch headers, newest first. Read-only.</summary>
public sealed class GetPayrollEntriesQueryHandler : IQueryHandler<GetPayrollEntriesQuery, IReadOnlyList<PayrollEntryDto>>
{
    private readonly IHrPayrollRepository _hr;

    public GetPayrollEntriesQueryHandler(IHrPayrollRepository hr)
    {
        _hr = hr;
    }

    public async Task<IReadOnlyList<PayrollEntryDto>> HandleAsync(
        GetPayrollEntriesQuery query,
        CancellationToken cancellationToken = default)
    {
        var entries = await _hr.GetPayrollEntriesByCompanyAsync(query.CompanyId, cancellationToken);
        var result = new List<PayrollEntryDto>(entries.Count);
        foreach (var entry in entries)
        {
            var slips = await _hr.GetSlipsByEntryAsync(entry.Id, cancellationToken);
            result.Add(PayrollEntryDto.Build(entry, slips.Count));
        }

        return result;
    }
}
