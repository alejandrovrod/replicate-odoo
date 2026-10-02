using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Accounts.Commands;

/// <summary>
/// Executes <see cref="CreateAccountCommand"/>: pure Domain validation (AccountValidator) plus the
/// two checks that need data access - duplicate AccountCode within the company and the ancestor
/// walk that prevents parent cycles (decision C5).
/// </summary>
public sealed class CreateAccountCommandHandler : ICommandHandler<CreateAccountCommand, Result<AccountDto>>
{
    private readonly IAccountRepository _accounts;

    public CreateAccountCommandHandler(IAccountRepository accounts)
    {
        _accounts = accounts;
    }

    public async Task<Result<AccountDto>> HandleAsync(CreateAccountCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            AccountValidator.EnsureValidFields(
                command.CompanyId,
                command.AccountCode,
                command.AccountName,
                command.RootType,
                command.Currency);

            IReadOnlyList<Account> ancestors = Array.Empty<Account>();

            if (command.ParentAccountId is { } parentId)
            {
                // Cycle prevention needs the loaded graph: walk from the proposed parent up to the
                // root and let AccountValidator reject any chain that loops (decision C5).
                ancestors = await _accounts.GetByIdWithAncestorsAsync(parentId, cancellationToken);

                if (ancestors.Count == 0)
                {
                    throw new AccountValidationException(
                        AccountErrorCodes.ParentNotFound,
                        $"Parent account '{parentId}' was not found in this company's Chart of Accounts.");
                }
            }

            var account = new Account
            {
                // Id is generated here so the cycle guard below can be exercised before insert.
                Id = Guid.NewGuid(),
                CompanyId = command.CompanyId,
                AccountCode = command.AccountCode.Trim(),
                AccountName = command.AccountName.Trim(),
                RootType = command.RootType,
                IsGroup = command.IsGroup,
                ParentAccountId = command.ParentAccountId,
                Currency = command.Currency.Trim(),
                IsActive = command.IsActive,

                // TenantId is intentionally NOT set: AppDbContext stamps CurrentTenantId on insert
                // and throws when no tenant context exists (Constitution Article II.4, fail closed).
            };

            if (ancestors.Count > 0)
            {
                AccountValidator.EnsureValidParent(account, ancestors[0]);

                var ancestorChain = new List<Guid>(ancestors.Count);
                foreach (var ancestor in ancestors)
                {
                    ancestorChain.Add(ancestor.Id);
                }

                AccountValidator.EnsureNoCycle(account.Id, ancestorChain);
            }

            if (await _accounts.ExistsByCodeAsync(account.CompanyId, account.AccountCode, cancellationToken))
            {
                throw new AccountValidationException(
                    AccountErrorCodes.DuplicateAccountCode,
                    $"Account code '{account.AccountCode}' already exists in this company's Chart of Accounts.");
            }

            await _accounts.AddAsync(account, cancellationToken);

            return Result<AccountDto>.Success(AccountDto.From(account));
        }
        catch (AccountValidationException ex)
        {
            return Result<AccountDto>.Failure(ex.Code, ex.Message);
        }
    }
}
