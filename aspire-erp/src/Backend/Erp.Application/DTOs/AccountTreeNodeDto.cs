using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>
/// One node of the nested COA tree returned by GetAccountTreeQuery
/// (id, code, name, rootType, isGroup, isActive, children[] - decision C7).
/// </summary>
public sealed record AccountTreeNodeDto(
    Guid Id,
    string Code,
    string Name,
    AccountRootType RootType,
    bool IsGroup,
    bool IsActive,
    IReadOnlyList<AccountTreeNodeDto> Children);
