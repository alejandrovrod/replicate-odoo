using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>Lists one company's payroll batch headers, newest first. Read-only.</summary>
public sealed class GetPayrollEntriesQueryHandler : IQueryHandler<GetPayrollEntriesQuery, PagedResult<PayrollEntryDto>>
{
    private readonly IHrPayrollRepository _hr;

    public GetPayrollEntriesQueryHandler(IHrPayrollRepository hr)
    {
        _hr = hr;
    }

    public async Task<PagedResult<PayrollEntryDto>> HandleAsync(
        GetPayrollEntriesQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _hr.GetPayrollEntriesByCompanyAsync(
            query.CompanyId,
            new PagedRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        var result = new List<PayrollEntryDto>(page.Items.Count);
        foreach (var entry in page.Items)
        {
            var slips = await _hr.GetSlipsByEntryAsync(entry.Id, cancellationToken);
            result.Add(PayrollEntryDto.Build(entry, slips.Count));
        }

        return page.Map(result);
    }
}
