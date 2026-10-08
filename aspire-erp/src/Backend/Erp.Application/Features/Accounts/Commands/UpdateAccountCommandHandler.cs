using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Accounts.Commands;

public sealed class UpdateAccountCommandHandler : ICommandHandler<UpdateAccountCommand, Result<AccountDto>>
{
    private readonly IAccountRepository _accounts;

    public UpdateAccountCommandHandler(IAccountRepository accounts)
    {
        _accounts = accounts;
    }

    public async Task<Result<AccountDto>> HandleAsync(UpdateAccountCommand command, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetByIdAsync(command.Id, cancellationToken);
        if (account is null || account.CompanyId != command.CompanyId)
        {
            return Result<AccountDto>.Failure(
                "account_not_found",
                $"Account '{command.Id}' not found.");
        }

        try
        {
            if (account.RowVersion is null || !account.RowVersion.AsSpan().SequenceEqual(command.RowVersion))
            {
                return Result<AccountDto>.Failure(
                    "concurrency_conflict",
                    $"The account '{account.AccountCode}' was modified by another user. Please refresh and try again.");
            }

            account.AccountName = command.AccountName.Trim();
            account.IsActive = command.IsActive;

            if (string.IsNullOrWhiteSpace(account.AccountName))
            {
                throw new AccountValidationException("missing_fields", "AccountName cannot be empty.");
            }

            await _accounts.UpdateAsync(account, cancellationToken);
            return Result<AccountDto>.Success(AccountDto.From(account));
        }
        catch (AccountValidationException ex)
        {
            return Result<AccountDto>.Failure(ex.Code, ex.Message);
        }
    }
}
