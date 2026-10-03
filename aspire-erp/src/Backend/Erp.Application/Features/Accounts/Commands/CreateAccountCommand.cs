using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;

namespace Erp.Application.Features.Accounts.Commands;

/// <summary>
/// Creates one account in a company's Chart of Accounts (vertical slice - plan.md 1.2).
/// Validates duplicate AccountCode per company and the parent rules, reporting domain failures
/// through <see cref="Result{T}"/> which Erp.Api maps to 400/409 ProblemDetails.
/// </summary>
/// <remarks>
/// Task 1.1: <c>Type</c> is OPTIONAL - backward compatibility for clients that do not send
/// `type`; the handler resolves the per-RootType default and documents it there
/// (Equity -> Equity, Income -> Revenue, Expense -> Expense, Asset/Liability -> Other).
/// </remarks>
public sealed record CreateAccountCommand(
    Guid CompanyId,
    string AccountCode,
    string AccountName,
    AccountRootType RootType,
    bool IsGroup,
    Guid? ParentAccountId,
    string Currency = "USD",
    bool IsActive = true,
    AccountType? Type = null) : ICommand<Result<AccountDto>>;
