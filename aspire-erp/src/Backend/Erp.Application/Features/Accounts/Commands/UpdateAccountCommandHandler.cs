using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Accounts.Commands;

/// <summary>
/// Executes <see cref="UpdateAccountCommand"/>: optimistic-concurrency gate (RowVersion is
/// required), pure Domain validation (AccountValidator) plus the checks that need data access -
/// the ancestor walk that prevents parent cycles and the children/GL-entry guards for IsGroup
/// conversions (decision C5, same pattern as CreateAccountCommandHandler).
/// </summary>
/// <remarks>
/// Immutability contract: TenantId, CompanyId, AccountCode, RootType and Type are never written
/// here. RootType equality is enforced on every reparent (EnsureValidParent), so a move can never
/// implicitly change a subtree's RootType - cross-RootType moves fail instead of cascading.
/// </remarks>
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
                AccountErrorCodes.AccountNotFound,
                $"Account '{command.Id}' not found.");
        }

        try
        {
            if (command.RowVersion is not { Length: > 0 }
                || account.RowVersion is not { Length: > 0 }
                || !account.RowVersion.AsSpan().SequenceEqual(command.RowVersion))
            {
                return Result<AccountDto>.Failure(
                    ConcurrencyErrorCodes.ConcurrencyConflict,
                    $"The account '{account.AccountCode}' was modified by another user. Please refresh and try again.");
            }

            // Constitution Article II.4: TenantId is stamped on insert and can never be altered.
            // The command carries no TenantId; capture the stored value and restore it after the
            // mutation so no future edit can leak a tenant change through this handler.
            var tenantId = account.TenantId;

            // Null-safe: a null name from the wire must fail as account_name_required (400),
            // not as a NullReferenceException (500).
            var accountName = (command.AccountName?.Trim()) ?? string.Empty;
            AccountValidator.EnsureValidFields(
                account.CompanyId,
                account.AccountCode,
                accountName,
                account.RootType,
                account.Type);

            if (command.IsGroup.HasValue && command.IsGroup.Value != account.IsGroup)
            {
                var hasChildren = await _accounts.HasChildrenAsync(account.Id, cancellationToken);
                var hasGlEntries = await _accounts.HasGlEntriesAsync(account.Id, cancellationToken);
                AccountValidator.EnsureValidIsGroupChange(
                    account.IsGroup, command.IsGroup.Value, hasChildren, hasGlEntries);
                account.IsGroup = command.IsGroup.Value;
            }

            if (command.ChangeParent)
            {
                ApplyParentChange(account, await ResolveParentChangeAsync(account, command, cancellationToken));
            }

            account.AccountName = accountName;
            account.IsActive = command.IsActive;

            // TenantId immutability backstop (see above): fail closed if anything moved it.
            account.TenantId = tenantId;

            await _accounts.UpdateAsync(account, cancellationToken);
            return Result<AccountDto>.Success(AccountDto.From(account));
        }
        catch (AccountValidationException ex)
        {
            return Result<AccountDto>.Failure(ex.Code, ex.Message);
        }
    }

    /// <summary>
    /// Validates the requested parent change and returns the new ParentAccountId (null = root).
    /// Reuses the create-path rules: the new parent must exist, be a group in the same company
    /// with the same RootType (no cascade - cross-RootType moves are rejected), and the ancestor
    /// walk from the new parent must not contain the account itself (reparenting under one of its
    /// own descendants is a cycle, caught the same way).
    /// </summary>
    private async Task<Guid?> ResolveParentChangeAsync(
        Account account,
        UpdateAccountCommand command,
        CancellationToken cancellationToken)
    {
        var newParentId = command.ParentAccountId;

        if (newParentId == account.Id)
        {
            throw new AccountValidationException(
                AccountErrorCodes.ParentIsSelf,
                "An account cannot be its own parent.");
        }

        if (newParentId is null)
        {
            // Moving to the root needs no parent validation; RootType is intrinsic to the
            // account, so the subtree keeps its RootType and no cascade is required.
            return null;
        }

        var ancestors = await _accounts.GetByIdWithAncestorsAsync(newParentId.Value, cancellationToken);
        if (ancestors.Count == 0)
        {
            throw new AccountValidationException(
                AccountErrorCodes.ParentNotFound,
                $"Parent account '{newParentId.Value}' was not found in this company's Chart of Accounts.");
        }

        var candidate = new Account
        {
            Id = account.Id,
            CompanyId = account.CompanyId,
            RootType = account.RootType,
            ParentAccountId = newParentId,
        };

        // ancestors[0] is the proposed parent itself (repository contract: self first).
        AccountValidator.EnsureValidParent(candidate, ancestors[0]);
        AccountValidator.EnsureNoCycle(
            account.Id,
            ancestors.Select(a => a.Id).ToList());

        return newParentId;
    }

    private static void ApplyParentChange(Account account, Guid? newParentId)
    {
        account.ParentAccountId = newParentId;
    }
}
