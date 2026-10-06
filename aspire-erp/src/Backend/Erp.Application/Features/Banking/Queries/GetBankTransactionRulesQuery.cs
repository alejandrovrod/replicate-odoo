using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;

namespace Erp.Application.Features.Banking.Queries;

/// <summary>Lists the heuristic rules of a company (Block C rule management read). Paginated (Standard Pagination Pattern): page 1 of 50 by default.</summary>
public sealed record GetBankTransactionRulesQuery(Guid CompanyId, int PageNumber = 1, int PageSize = 50)
    : IQuery<PagedResult<BankTransactionRuleDto>>;
