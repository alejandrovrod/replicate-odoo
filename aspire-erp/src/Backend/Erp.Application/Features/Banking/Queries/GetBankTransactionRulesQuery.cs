using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Banking.Queries;

/// <summary>Lists the heuristic rules of a company (Block C rule management read).</summary>
public sealed record GetBankTransactionRulesQuery(Guid CompanyId)
    : IQuery<IReadOnlyList<BankTransactionRuleDto>>;
