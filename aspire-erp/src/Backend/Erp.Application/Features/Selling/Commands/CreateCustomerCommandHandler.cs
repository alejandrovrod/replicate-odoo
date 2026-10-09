using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Selling.Commands;

/// <summary>
/// Executes <see cref="CreateCustomerCommand"/>: pure Domain validation (CustomerValidator), the
/// duplicate-code-per-company rule, the optional receivable-account lookup and persistence
/// (Task 5.1 acceptance).
/// </summary>
public sealed class CreateCustomerCommandHandler : ICommandHandler<CreateCustomerCommand, Result<CustomerDto>>
{
    private readonly ICustomerRepository _customers;
    private readonly IAccountRepository _accounts;
    private readonly ICurrencyRepository _currencies;

    public CreateCustomerCommandHandler(
        ICustomerRepository customers,
        IAccountRepository accounts,
        ICurrencyRepository currencies)
    {
        _customers = customers;
        _accounts = accounts;
        _currencies = currencies;
    }

    public async Task<Result<CustomerDto>> HandleAsync(
        CreateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // plan.md §1 TaxId is NVARCHAR(50) NOT NULL with no server default: normalize an
            // omitted/null value to the empty string instead of letting the insert fail.
            var taxId = command.TaxId ?? string.Empty;

            // ERPNext parity: an omitted CustomerType falls back to "Company" (ERPNext default).
            var customerType = string.IsNullOrWhiteSpace(command.CustomerType) ? "Company" : command.CustomerType.Trim();

            CustomerValidator.EnsureValidCustomerFields(
                command.CompanyId,
                command.CustomerCode,
                command.CustomerName,
                taxId,
                command.CreditLimit,
                command.PaymentTermsDays,
                customerType,
                command.CustomerGroup,
                command.Territory,
                command.BillingAddress,
                command.Phone,
                command.Email,
                command.ContactPerson,
                command.Website,
                command.PaymentTerms,
                command.CustomerDetails);

            if (command.CurrencyId.HasValue
                && await _currencies.GetByIdAsync(command.CurrencyId.Value, cancellationToken) is null)
            {
                throw new CustomerValidationException(
                    CurrencyErrorCodes.CurrencyNotFound,
                    $"Currency '{command.CurrencyId.Value}' was not found.");
            }

            var code = command.CustomerCode.Trim();

            if (await _customers.ExistsCodeAsync(command.CompanyId, code, cancellationToken))
            {
                throw new CustomerValidationException(
                    SellingErrorCodes.DuplicateCustomerCode,
                    $"Customer code '{code}' already exists in this company.");
            }

            if (command.DefaultReceivableAccountId is { } receivableAccountId)
            {
                // The FK (FK_Customer_Account) would otherwise surface as a raw SQL error: the
                // referenced account must exist in this tenant AND belong to the same company
                // (mirrors how CreateAccountCommandHandler resolves an optional parent).
                var receivableAccount = await _accounts.GetByIdAsync(receivableAccountId, cancellationToken);

                if (receivableAccount is null || receivableAccount.CompanyId != command.CompanyId)
                {
                    throw new CustomerValidationException(
                        SellingErrorCodes.InvalidReceivableAccount,
                        $"Receivable account '{receivableAccountId}' was not found in this company.");
                }
            }

            var customer = new Customer
            {
                // Id is generated here so tests can inspect the entity before it is persisted.
                Id = Guid.NewGuid(),
                CompanyId = command.CompanyId,
                CustomerCode = code,
                CustomerName = command.CustomerName.Trim(),
                TaxId = taxId.Trim(),
                DefaultReceivableAccountId = command.DefaultReceivableAccountId,
                CreditLimit = command.CreditLimit,
                BypassCreditLimitCheck = command.BypassCreditLimitCheck,
                CurrencyId = command.CurrencyId,
                PaymentTermsDays = command.PaymentTermsDays,
                IsActive = command.IsActive,
                CustomerType = customerType,
                CustomerGroup = (command.CustomerGroup ?? string.Empty).Trim(),
                Territory = (command.Territory ?? string.Empty).Trim(),
                BillingAddress = (command.BillingAddress ?? string.Empty).Trim(),
                Phone = (command.Phone ?? string.Empty).Trim(),
                Email = (command.Email ?? string.Empty).Trim(),
                ContactPerson = (command.ContactPerson ?? string.Empty).Trim(),
                Website = (command.Website ?? string.Empty).Trim(),
                PaymentTerms = (command.PaymentTerms ?? string.Empty).Trim(),
                CustomerDetails = (command.CustomerDetails ?? string.Empty).Trim(),

                // TenantId is intentionally NOT set: AppDbContext stamps CurrentTenantId on insert
                // and throws when no tenant context exists (Constitution Article II.4, fail closed).
                // OutstandingAmount starts at 0.0000; only ledger postings (Task 5.3) move it.
            };

            await _customers.AddAsync(customer, cancellationToken);

            return Result<CustomerDto>.Success(CustomerDto.From(customer));
        }
        catch (CustomerValidationException ex)
        {
            return Result<CustomerDto>.Failure(ex.Code, ex.Message);
        }
    }
}
