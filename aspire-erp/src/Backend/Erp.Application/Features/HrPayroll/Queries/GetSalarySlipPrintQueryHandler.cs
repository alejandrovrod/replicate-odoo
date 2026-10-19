using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.HrPayroll.Queries;

/// <summary>
/// Executes <see cref="GetSalarySlipPrintQuery"/>: resolves the run (company-scoped), the slip
/// inside it, the employee it belongs to and the company fiscal header, then assembles the
/// printable DTO. Read-only; any missing piece yields null so the controller can answer 404.
/// </summary>
public sealed class GetSalarySlipPrintQueryHandler
    : IQueryHandler<GetSalarySlipPrintQuery, SalarySlipPrintableDto?>
{
    private readonly IHrPayrollRepository _hr;
    private readonly ICompanyRepository _companies;

    public GetSalarySlipPrintQueryHandler(IHrPayrollRepository hr, ICompanyRepository companies)
    {
        _hr = hr;
        _companies = companies;
    }

    public async Task<SalarySlipPrintableDto?> HandleAsync(
        GetSalarySlipPrintQuery query,
        CancellationToken cancellationToken = default)
    {
        var entry = await _hr.GetPayrollEntryByIdAsync(query.PayrollEntryId, cancellationToken);
        if (entry is null || entry.CompanyId != query.CompanyId)
        {
            return null;
        }

        var slips = await _hr.GetSlipsByEntryAsync(entry.Id, cancellationToken);
        var slip = slips.FirstOrDefault(s => s.Id == query.SlipId);
        if (slip is null)
        {
            return null;
        }

        var company = await _companies.GetByIdAsync(query.CompanyId, cancellationToken);
        if (company is null)
        {
            return null;
        }

        var employee = await _hr.GetEmployeeByIdAsync(slip.EmployeeId, cancellationToken);

        return SalarySlipPrintableDto.Build(entry, slip, company, employee);
    }
}
