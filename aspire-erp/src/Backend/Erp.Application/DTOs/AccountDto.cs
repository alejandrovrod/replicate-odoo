using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>Created-account payload returned by POST /api/v1/accounts (201 Created).</summary>
public sealed record AccountDto(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Name,
    AccountRootType RootType,
    bool IsGroup,
    Guid? ParentAccountId,
    string Currency,
    bool IsActive)
{
    public static AccountDto From(Account account) =>
        new(
            account.Id,
            account.CompanyId,
            account.AccountCode,
            account.AccountName,
            account.RootType,
            account.IsGroup,
            account.ParentAccountId,
            account.Currency,
            account.IsActive);
}
