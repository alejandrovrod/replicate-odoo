using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Accounts.Queries;

/// <summary>
/// Builds the nested COA tree: loads the company's accounts flat through the repository (tenant
/// filtering is automatic - Constitution II.3) and assembles the hierarchy in memory, ordered by
/// AccountCode. A stored parent cycle can never be reached from a root node, so the assembly
/// terminates without extra guarding.
/// </summary>
public sealed class GetAccountTreeQueryHandler : IQueryHandler<GetAccountTreeQuery, IReadOnlyList<AccountTreeNodeDto>>
{
    private readonly IAccountRepository _accounts;

    public GetAccountTreeQueryHandler(IAccountRepository accounts)
    {
        _accounts = accounts;
    }

    public async Task<IReadOnlyList<AccountTreeNodeDto>> HandleAsync(
        GetAccountTreeQuery query,
        CancellationToken cancellationToken = default)
    {
        var all = await _accounts.GetByCompanyAsync(query.CompanyId, cancellationToken);

        var ids = new HashSet<Guid>(all.Count);
        foreach (var account in all)
        {
            ids.Add(account.Id);
        }

        var childrenByParent = new Dictionary<Guid, List<Account>>();
        foreach (var account in all)
        {
            if (account.ParentAccountId is not { } parentId || !ids.Contains(parentId))
            {
                continue;
            }

            if (!childrenByParent.TryGetValue(parentId, out var children))
            {
                children = new List<Account>();
                childrenByParent[parentId] = children;
            }

            children.Add(account);
        }

        AccountTreeNodeDto BuildNode(Account account)
        {
            List<AccountTreeNodeDto>? childNodes = null;

            if (childrenByParent.TryGetValue(account.Id, out var children))
            {
                children.Sort((x, y) => string.CompareOrdinal(x.AccountCode, y.AccountCode));
                childNodes = new List<AccountTreeNodeDto>(children.Count);
                foreach (var child in children)
                {
                    childNodes.Add(BuildNode(child));
                }
            }

            return new AccountTreeNodeDto(
                account.Id,
                account.AccountCode,
                account.AccountName,
                account.RootType,
                account.IsGroup,
                account.IsActive,
                childNodes ?? (IReadOnlyList<AccountTreeNodeDto>)Array.Empty<AccountTreeNodeDto>());
        }

        var roots = new List<Account>();
        foreach (var account in all)
        {
            if (account.ParentAccountId is null || !ids.Contains(account.ParentAccountId.Value))
            {
                roots.Add(account);
            }
        }

        roots.Sort((x, y) => string.CompareOrdinal(x.AccountCode, y.AccountCode));

        var tree = new List<AccountTreeNodeDto>(roots.Count);
        foreach (var root in roots)
        {
            tree.Add(BuildNode(root));
        }

        return tree;
    }
}
