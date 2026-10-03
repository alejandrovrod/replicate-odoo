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
            // Task 1.1: Type is optional on the wire. An explicit client value wins; otherwise
            // the per-RootType default keeps every stored row's Type NVARCHAR(50) NOT NULL
            // (plan.md §7.3) with a sensible ERPNext-compatible classification:
            //   Equity -> Equity, Income -> Revenue, Expense -> Expense, and Other for the
            //   heterogeneous Asset/Liability roots (no single ERPNext account_type covers
            //   Bank/Cash/Receivable/Stock on one side and Payable on the other).
            AccountType type = command.Type ?? DefaultTypeFor(command.RootType);

            AccountValidator.EnsureValidFields(
                command.CompanyId,
                command.AccountCode,
                command.AccountName,
                command.RootType,
                command.Currency,
                type);

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
                Type = type,
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

    /// <summary>
    /// Per-RootType default for the optional <c>Type</c> (Task 1.1): Equity -> Equity,
    /// Income -> Revenue, Expense -> Expense, and Other for Asset/Liability (no single ERPNext
    /// account_type covers those two heterogeneous roots - clients pick Bank/Cash/Receivable/
    /// Stock/Payable explicitly when it applies). Must stay aligned with the backfill mapping
    /// documented in the AddAccountTypeAndUniqueCode migration.
    /// </summary>
    private static AccountType DefaultTypeFor(AccountRootType rootType) => rootType switch
    {
        AccountRootType.Equity => AccountType.Equity,
        AccountRootType.Income => AccountType.Revenue,
        AccountRootType.Expense => AccountType.Expense,
        _ => AccountType.Other,
    };
}
