using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Erp.Application.Common;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.SystemBase.Companies;

/// <summary>
/// Assembles <see cref="GetCompaniesQuery"/> from <see cref="ICompanyRepository"/> (the tenant
/// filter is applied by the repository, so the caller only sees its own companies).
/// </summary>
public class GetCompaniesQueryHandler : IQueryHandler<GetCompaniesQuery, IReadOnlyList<CompanyDto>>
{
    private readonly ICompanyRepository _companyRepository;

    public GetCompaniesQueryHandler(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository;
    }

    public async Task<IReadOnlyList<CompanyDto>> HandleAsync(
        GetCompaniesQuery query,
        CancellationToken cancellationToken = default)
    {
        var companies = await _companyRepository.GetAllAsync(cancellationToken);

        return companies
            .Select(company => new CompanyDto
            {
                Id = company.Id,
                Name = company.Name,
                FrozenAccountsDate = company.FrozenAccountsDate,
                DefaultRetainedEarningsAccountId = company.DefaultRetainedEarningsAccountId,
                DefaultRetainedEarningsAccountCode = company.DefaultRetainedEarningsAccountCode,
            })
            .ToList();
    }
}
