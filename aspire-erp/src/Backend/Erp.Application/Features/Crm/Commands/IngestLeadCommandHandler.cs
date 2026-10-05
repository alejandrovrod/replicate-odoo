using Erp.Application.Common;
using Erp.Application.Features.Crm.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Erp.Application.Features.Crm.Commands;

public sealed class IngestLeadCommandHandler : ICommandHandler<IngestLeadCommand, Result<IngestLeadResultDto>>
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

    public async Task<Result<IngestLeadResultDto>> HandleAsync(IngestLeadCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var company = await _companyRepository.GetByIdAsync(command.CompanyId, cancellationToken)
                ?? throw new CRMValidationException(
                    CRMErrorCodes.CompanyRequired,
                    $"Company '{command.CompanyId}' not found.");

            // Spec CRM-04 replay: the (CompanyId, Source, DeduplicationKey) triple already
            // exists - return it untouched (zero writes). A null/empty key means "no dedup
            // requested" (manual entry): always insert.
            if (!string.IsNullOrWhiteSpace(command.DeduplicationKey))
            {
                var existing = await _crmRepository.GetLeadByDedupKeyAsync(
                    command.CompanyId,
                    command.Source,
                    command.DeduplicationKey!,
                    cancellationToken);

                if (existing is not null)
                {
                    return Result<IngestLeadResultDto>.Success(
                        new IngestLeadResultDto(LeadDto.Build(existing), Duplicate: true));
                }
            }

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
                ExternalReference = string.IsNullOrWhiteSpace(command.DeduplicationKey)
                    ? null
                    : command.DeduplicationKey,
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

            return Result<IngestLeadResultDto>.Success(
                new IngestLeadResultDto(LeadDto.Build(lead), Duplicate: false));
        }
        catch (DuplicateLeadException ex)
        {
            // Fix-pass W1: the filtered unique index UQ_Lead_Company_Source_ExternalRef
            // rejected our insert because a concurrent ingest won the race (the
            // CustomerRepository duplicate-code translation precedent). Re-read the winner
            // and answer Duplicate=true with zero new rows; a missing winner means the
            // index fired for another reason, so let it surface loudly.
            var winner = await _crmRepository.GetLeadByDedupKeyAsync(
                ex.CompanyId,
                ex.LeadSource,
                ex.ExternalReference,
                cancellationToken);

            if (winner is not null)
            {
                return Result<IngestLeadResultDto>.Success(
                    new IngestLeadResultDto(LeadDto.Build(winner), Duplicate: true));
            }

            throw;
        }
        catch (CRMValidationException ex)
        {
            return Result<IngestLeadResultDto>.Failure(ex.Code, ex.Message);
        }
    }
}
