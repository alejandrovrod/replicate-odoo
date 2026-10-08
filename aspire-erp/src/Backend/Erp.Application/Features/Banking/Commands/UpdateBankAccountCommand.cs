using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Banking.Commands;

/// <summary>
/// Updates one bank account master row. Carries the original <c>RowVersion</c> for optimistic
/// concurrency: a stale token fails with <c>concurrency_conflict</c> (409). Balance and
/// reconciliation-date fields are NOT editable here - only reconciliation flows move them.
/// </summary>
public sealed record UpdateBankAccountCommand(
    Guid Id,
    Guid CompanyId,
    string AccountName,
    string BankName,
    string AccountNumber,
    Guid GLAccountId,
    Guid? CurrencyId,
    bool IsActive,
    byte[] RowVersion) : ICommand<Result<BankAccountDto>>;
