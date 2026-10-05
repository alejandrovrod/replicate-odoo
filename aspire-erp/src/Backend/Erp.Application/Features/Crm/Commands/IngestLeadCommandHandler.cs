using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Erp.Application.Features.Crm.Commands;

public sealed class IngestLeadCommandHandler : ICommandHandler<IngestLeadCommand, Result<Guid>>
{
    private readonly ICrmRepository _crmRepository;
    private readonly ICompanyRepository _companyRepository;

    public IngestLeadCommandHandler(
        ICrmRepository crmRepository,
        ICompanyRepository companyRepository)
    {
        _crmRepository = crmRepository;
        _companyRepository = companyRepository;
    }

    public async Task<Result<Guid>> HandleAsync(IngestLeadCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var company = await _companyRepository.GetByIdAsync(command.CompanyId, cancellationToken)
                ?? throw new CRMValidationException(
                    CRMErrorCodes.CompanyRequired,
                    $"Company '{command.CompanyId}' not found.");

            var lead = new Lead
            {
                Id = Guid.NewGuid(),
                CompanyId = command.CompanyId,
                TenantId = company.TenantId,
                LeadCode = command.LeadCode,
                LeadName = command.LeadName,
                OrganizationName = command.OrganizationName,
                Email = command.Email,
                Phone = command.Phone,
                Source = command.Source,
                Status = LeadStatus.Open,
                IsActive = true
            };

            LeadValidator.EnsureValidLeadFields(
                lead.CompanyId,
                lead.LeadCode,
                lead.LeadName,
                lead.Email,
                lead.Phone,
                lead.Source);

            await _crmRepository.AddLeadAsync(lead, cancellationToken);

            return Result<Guid>.Success(lead.Id);
        }
        catch (CRMValidationException ex)
        {
            return Result<Guid>.Failure(ex.Code, ex.Message);
        }
    }
}
