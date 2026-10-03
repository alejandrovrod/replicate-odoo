using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>Created-account payload returned by POST /api/v1/accounts (201 Created).</summary>
/// <remarks>
/// <c>Type</c> mirrors <c>RootType</c> (Task 1.1): both are enums serialized by Erp.Api's
/// JsonStringEnumConverter as camelCase names ("rootType": "Asset", "type": "Cash"), matching
/// how the values are stored in the NVARCHAR columns (plan.md §7.3).
/// </remarks>
public sealed record AccountDto(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Name,
    AccountRootType RootType,
    AccountType Type,
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
            account.Type,
            account.IsGroup,
            account.ParentAccountId,
            account.Currency,
            account.IsActive);
}
