using Erp.Application.Common;
using Erp.Application.Features.Crm.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Erp.Application.Features.Crm.Commands;

public sealed class ConvertLeadCommandHandler : ICommandHandler<ConvertLeadCommand, Result<ConvertLeadResultDto>>
{
    private readonly ICrmRepository _crmRepository;
    private readonly ICrmActivityRepository _activityRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly ICurrencyRepository _currencies;

    public ConvertLeadCommandHandler(
        ICrmRepository crmRepository,
        ICrmActivityRepository activityRepository,
        ICustomerRepository customerRepository,
        ICompanyRepository companyRepository,
        ICurrencyRepository currencies)
    {
        _crmRepository = crmRepository;
        _activityRepository = activityRepository;
        _customerRepository = customerRepository;
        _companyRepository = companyRepository;
        _currencies = currencies;
    }

    public async Task<Result<ConvertLeadResultDto>> HandleAsync(ConvertLeadCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var company = await _companyRepository.GetByIdAsync(command.CompanyId, cancellationToken)
                ?? throw new CRMValidationException(
                    CRMErrorCodes.CompanyRequired,
                    $"Company '{command.CompanyId}' not found.");

            var lead = await _crmRepository.GetLeadByIdAsync(command.LeadId, cancellationToken)
                ?? throw new CRMValidationException(
                    "crm_lead_not_found",
                    $"Lead '{command.LeadId}' not found.");

            if (lead.CompanyId != command.CompanyId)
            {
                throw new CRMValidationException(
                    "crm_lead_company_mismatch",
                    "Lead does not belong to the specified company.");
            }

            // Verify customer code is unique
            if (await _customerRepository.ExistsCodeAsync(command.CompanyId, command.CustomerCode, cancellationToken))
            {
                throw new CRMValidationException(
                    "crm_customer_code_exists",
                    $"Customer code '{command.CustomerCode}' is already in use.");
            }

            var closingDate = command.ExpectedClosingDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
            var oppNumber = await _crmRepository.NextOpportunityNumberAsync(company.Id, closingDate.Year, cancellationToken);

            if (command.DefaultCurrencyId.HasValue
                && await _currencies.GetByIdAsync(command.DefaultCurrencyId.Value, cancellationToken) is null)
            {
                throw new CRMValidationException(
                    CurrencyErrorCodes.CurrencyNotFound,
                    $"Currency '{command.DefaultCurrencyId.Value}' was not found.");
            }

            var resultDto = await _crmRepository.ExecuteInTransactionAsync(async token =>
            {
                // 1. Create the Customer
                var customer = new Customer
                {
                    Id = Guid.NewGuid(),
                    TenantId = lead.TenantId,
                    CompanyId = lead.CompanyId,
                    CustomerCode = command.CustomerCode,
                    CustomerName = lead.OrganizationName ?? lead.LeadName,
                    TaxId = string.Empty,
                    CreditLimit = 0m,
                    CurrencyId = command.DefaultCurrencyId,
                    PaymentTermsDays = command.PaymentTermsDays,
                    IsActive = true
                };

                CustomerValidator.EnsureValidCustomerFields(
                    customer.CompanyId,
                    customer.CustomerCode,
                    customer.CustomerName,
                    customer.TaxId,
                    customer.CreditLimit,
                    customer.PaymentTermsDays);

                await _customerRepository.AddAsync(customer, token);

                // 2. Create the Opportunity
                var opportunity = new Opportunity
                {
                    Id = Guid.NewGuid(),
                    TenantId = lead.TenantId,
                    CompanyId = lead.CompanyId,
                    OpportunityNumber = oppNumber,
                    OpportunityFrom = "Lead",
                    PartyId = lead.Id,
                    PartyName = customer.CustomerName,
                    Stage = OpportunityStage.Qualification,
                    OpportunityAmount = command.OpportunityAmount,
                    Probability = command.OpportunityProbability,
                    CurrencyId = command.DefaultCurrencyId,
                    ExpectedClosingDate = closingDate,
                    Status = OpportunityStatus.Open,
                    AssignedSalespersonId = lead.AssignedToUserId
                };

                OpportunityValidator.EnsureValidOpportunityFields(
                    opportunity.CompanyId,
                    opportunity.OpportunityNumber,
                    opportunity.OpportunityFrom,
                    opportunity.PartyId,
                    opportunity.PartyName,
                    opportunity.OpportunityAmount,
                    opportunity.Probability);

                await _crmRepository.AddOpportunityAsync(opportunity, token);

                // 3. Mark Lead as Converted
                lead.MarkAsConverted(opportunity.Id, customer.Id);
                await _crmRepository.UpdateLeadAsync(lead, token);

                // 4. Spec CRM-03 audit trail: one Note on the new opportunity referencing the
                // source lead, so history survives the conversion boundary. Author is the
                // caller when known, else the lead's assignee; with neither known there is no
                // attributable author (CRMActivityValidator forbids empty) so the note is
                // omitted - the Customer+Opportunity+Converted writes still commit.
                var authorId = command.ConvertedByUserId ?? lead.AssignedToUserId;
                if (authorId.HasValue && authorId.Value != Guid.Empty)
                {
                    await _activityRepository.AddActivityAsync(new CRMActivity
                    {
                        Id = Guid.NewGuid(),
                        OpportunityId = opportunity.Id,
                        Type = CRMActivityType.Note,
                        Subject = $"Converted from lead {lead.LeadCode}",
                        Content = $"Converted from lead {lead.LeadCode} ({lead.LeadName})"
                            + (string.IsNullOrWhiteSpace(lead.OrganizationName)
                                ? string.Empty
                                : $" at {lead.OrganizationName}")
                            + $" as customer {customer.CustomerCode}.",
                        ActivityDate = DateTimeOffset.UtcNow,
                        CreatedByUserId = authorId.Value
                    }, token);
                }

                return new ConvertLeadResultDto(
                    lead.Id,
                    customer.Id,
                    customer.CustomerCode,
                    opportunity.Id,
                    opportunity.OpportunityNumber,
                    opportunity.WeightedAmount
                );

            }, cancellationToken);

            return Result<ConvertLeadResultDto>.Success(resultDto);
        }
        catch (CRMValidationException ex)
        {
            return Result<ConvertLeadResultDto>.Failure(ex.Code, ex.Message);
        }
        catch (CustomerValidationException ex)
        {
            return Result<ConvertLeadResultDto>.Failure(ex.Code, ex.Message);
        }
    }
}
