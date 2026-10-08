using System;
using System.Threading;
using System.Threading.Tasks;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.SystemBase.Companies;

public class UpdateCompanyCommandHandler : ICommandHandler<UpdateCompanyCommand, Result<CompanyDto>>
{
    private readonly ICompanyRepository _companyRepository;

    public UpdateCompanyCommandHandler(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository;
    }

    public async Task<Result<CompanyDto>> HandleAsync(UpdateCompanyCommand command, CancellationToken cancellationToken = default)
    {
        var company = await _companyRepository.GetByIdAsync(command.Id, cancellationToken);
        if (company == null) return Result<CompanyDto>.Failure(new Error("Company.NotFound", "Company not found."));

        company.FrozenAccountsDate = command.FrozenAccountsDate;
        company.DefaultRetainedEarningsAccountId = command.DefaultRetainedEarningsAccountId;

        await _companyRepository.UpdateCompanyAsync(company, cancellationToken);

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
