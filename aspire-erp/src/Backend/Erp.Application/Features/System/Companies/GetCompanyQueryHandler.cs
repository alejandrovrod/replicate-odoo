using System;
using System.Threading;
using System.Threading.Tasks;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.SystemBase.Companies;

public class GetCompanyQueryHandler : IQueryHandler<GetCompanyQuery, Result<CompanyDto>>
{
    private readonly ICompanyRepository _companyRepository;

    public GetCompanyQueryHandler(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository;
    }

    public async Task<Result<CompanyDto>> HandleAsync(GetCompanyQuery query, CancellationToken cancellationToken = default)
    {
        var company = await _companyRepository.GetByIdAsync(query.Id, cancellationToken);
        if (company == null) return Result<CompanyDto>.Failure(new Error("Company.NotFound", "Company not found."));

        return Result<CompanyDto>.Success(new CompanyDto
        {
            Id = company.Id,
            Name = company.Name,
            FrozenAccountsDate = company.FrozenAccountsDate,
            DefaultRetainedEarningsAccountId = company.DefaultRetainedEarningsAccountId,
            DefaultRetainedEarningsAccountCode = company.DefaultRetainedEarningsAccountCode
        });
    }
}
