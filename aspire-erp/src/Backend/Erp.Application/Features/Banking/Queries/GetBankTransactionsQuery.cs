using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;

namespace Erp.Application.Features.Banking.Queries;

/// <summary>Lists staging bank lines of a company (optional account / status filter). Paginated (Standard Pagination Pattern): page 1 of 50 by default.</summary>
public sealed record GetBankTransactionsQuery(
    Guid CompanyId,
    Guid? BankAccountId = null,
    BankTransactionStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 50) : IQuery<PagedResult<BankTransactionDto>>;
