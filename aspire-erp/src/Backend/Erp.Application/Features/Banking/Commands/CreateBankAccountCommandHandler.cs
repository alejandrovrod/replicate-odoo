using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Banking.Commands;

/// <summary>
/// Executes <see cref="CreateBankAccountCommand"/>: field validation, the company/GL-account/
/// currency lookups that need data access, and persistence.
/// </summary>
public sealed class CreateBankAccountCommandHandler : ICommandHandler<CreateBankAccountCommand, Result<BankAccountDto>>
{
    private readonly IBankRepository _banks;
    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly ICurrencyRepository _currencies;

    public CreateBankAccountCommandHandler(
        IBankRepository banks,
        ICompanyRepository companies,
        IAccountRepository accounts,
        ICurrencyRepository currencies)
    {
        _banks = banks;
        _companies = companies;
        _accounts = accounts;
        _currencies = currencies;
    }

    public async Task<Result<BankAccountDto>> HandleAsync(
        CreateBankAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var company = await _companies.GetByIdAsync(command.CompanyId, cancellationToken)
                ?? throw new BankingValidationException(
                    BankingErrorCodes.CompanyNotFound,
                    $"Company '{command.CompanyId}' was not found in this tenant.");

            // bank-catalog spec §4: optional catalog link. Unknown/inactive/wrong-country ->
            // bank_not_found; empty BankName autocompletes from the official catalog name;
            // no link -> free-text BankName validated exactly as before (BNK-G3).
            Bank? catalogBank = null;
            if (command.BankId.HasValue)
            {
                catalogBank = await BankAccountGuards.RequireCatalogBankAsync(
                    _banks, command.BankId.Value, company.CountryCode, cancellationToken);
            }

            var bankName = BankAccountGuards.ResolveBankDisplayName(command.BankName, catalogBank);

            BankAccountGuards.EnsureValidFields(
                command.CompanyId,
                command.AccountName,
                bankName,
                command.AccountNumber);

            await BankAccountGuards.RequirePostableGlAccountAsync(
                _accounts, command.GLAccountId, command.CompanyId, cancellationToken);

            if (command.CurrencyId.HasValue
                && await _currencies.GetByIdAsync(command.CurrencyId.Value, cancellationToken) is null)
            {
                throw new BankingValidationException(
                    CurrencyErrorCodes.CurrencyNotFound,
                    $"Currency '{command.CurrencyId.Value}' was not found.");
            }

            var account = new BankAccount
            {
                Id = Guid.NewGuid(),
                CompanyId = command.CompanyId,
                AccountName = command.AccountName.Trim(),
                BankName = bankName.Trim(),
                BankId = command.BankId,
                AccountNumber = command.AccountNumber.Trim(),
                GLAccountId = command.GLAccountId,
                CurrencyId = command.CurrencyId,
                LastReconciledBalance = 0m,
                IsActive = command.IsActive,
            };

            await _banks.AddAccountAsync(account, cancellationToken);

            return Result<BankAccountDto>.Success(BankAccountDto.From(account));
        }
        catch (BankingValidationException ex)
        {
            return Result<BankAccountDto>.Failure(ex.Code, ex.Message);
        }
    }
}
