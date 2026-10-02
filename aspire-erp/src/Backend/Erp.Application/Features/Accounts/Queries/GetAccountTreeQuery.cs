using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Accounts.Queries;

/// <summary>
/// Loads the full Chart of Accounts of one company as a nested hierarchy (a company owns its COA -
/// ubiquitous language, .specify/domain_business_rules_ddd.md 1). Unknown/empty companies simply
/// yield an empty tree.
/// </summary>
public sealed record GetAccountTreeQuery(Guid CompanyId) : IQuery<IReadOnlyList<AccountTreeNodeDto>>;
