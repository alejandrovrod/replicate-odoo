using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;

namespace Erp.Application.Features.Banking.Queries;

/// <summary>Lists staging bank lines of a company (optional account / status filter).</summary>
public sealed record GetBankTransactionsQuery(
    Guid CompanyId,
    Guid? BankAccountId = null,
    BankTransactionStatus? Status = null) : IQuery<IReadOnlyList<BankTransactionDto>>;
