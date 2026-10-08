using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>
/// One node of the nested COA tree returned by GetAccountTreeQuery
/// (id, code, name, rootType, isGroup, isActive, children[] - decision C7).
/// <c>RowVersion</c> travels so the edit modal can send the optimistic-concurrency
/// token back on PUT without an extra fetch.
/// </summary>
public sealed record AccountTreeNodeDto(
    Guid Id,
    string Code,
    string Name,
    AccountRootType RootType,
    bool IsGroup,
    bool IsActive,
    AccountType Type,
    IReadOnlyList<AccountTreeNodeDto> Children,
    byte[]? RowVersion = null);
