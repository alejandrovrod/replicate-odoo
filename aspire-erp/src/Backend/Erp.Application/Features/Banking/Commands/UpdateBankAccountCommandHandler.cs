using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Banking.Commands;

/// <summary>
/// Executes <see cref="UpdateBankAccountCommand"/>: existence + company scoping, fail-fast
/// RowVersion compare-and-swap, field validation, the GL-account/currency lookups and
/// persistence.
/// </summary>
public sealed class UpdateBankAccountCommandHandler : ICommandHandler<UpdateBankAccountCommand, Result<BankAccountDto>>
{
    private readonly IBankRepository _banks;
    private readonly IAccountRepository _accounts;
    private readonly ICurrencyRepository _currencies;

    public UpdateBankAccountCommandHandler(
        IBankRepository banks,
        IAccountRepository accounts,
        ICurrencyRepository currencies)
    {
        _banks = banks;
        _accounts = accounts;
        _currencies = currencies;
    }

    public async Task<Result<BankAccountDto>> HandleAsync(
        UpdateBankAccountCommand request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var account = await _banks.GetAccountByIdAsync(request.Id, cancellationToken);
            if (account is null || account.CompanyId != request.CompanyId)
            {
                throw new BankingValidationException(
                    BankingErrorCodes.BankAccountNotFound,
                    $"Bank account '{request.Id}' was not found in this company.");
            }

            if (account.RowVersion is null || !account.RowVersion.AsSpan().SequenceEqual(request.RowVersion))
            {
                throw new ConcurrencyConflictException(nameof(BankAccount), account.Id);
            }

            BankAccountGuards.EnsureValidFields(
                request.CompanyId,
                request.AccountName,
                request.BankName,
                request.AccountNumber);

            await BankAccountGuards.RequirePostableGlAccountAsync(
                _accounts, request.GLAccountId, request.CompanyId, cancellationToken);

            if (request.CurrencyId.HasValue
                && await _currencies.GetByIdAsync(request.CurrencyId.Value, cancellationToken) is null)
            {
                throw new BankingValidationException(
                    CurrencyErrorCodes.CurrencyNotFound,
                    $"Currency '{request.CurrencyId.Value}' was not found.");
            }

            account.AccountName = request.AccountName.Trim();
            account.BankName = request.BankName.Trim();
            account.AccountNumber = request.AccountNumber.Trim();
            account.GLAccountId = request.GLAccountId;
            account.CurrencyId = request.CurrencyId;
            account.IsActive = request.IsActive;

            await _banks.UpdateAccountAsync(account, cancellationToken);

            return Result<BankAccountDto>.Success(BankAccountDto.From(account));
        }
        catch (BankingValidationException ex)
        {
            return Result<BankAccountDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<BankAccountDto>.Failure(ex.Code, ex.Message);
        }
    }
}
