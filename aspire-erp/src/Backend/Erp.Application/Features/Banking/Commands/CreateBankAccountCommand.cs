using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Banking.Commands;

/// <summary>
/// Creates one bank account master row (links an external account number to a postable GL
/// account of the same company). The linked GL account and the currency are validated for
/// existence; failures surface as domain failures through <see cref="Result{T}"/>.
/// </summary>
public sealed record CreateBankAccountCommand(
    Guid CompanyId,
    string AccountName,
    string BankName,
    string AccountNumber,
    Guid GLAccountId,
    Guid? CurrencyId = null,
    bool IsActive = true) : ICommand<Result<BankAccountDto>>;
