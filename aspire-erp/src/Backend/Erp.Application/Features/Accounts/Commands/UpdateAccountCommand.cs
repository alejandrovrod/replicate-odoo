using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;

namespace Erp.Application.Features.Accounts.Commands;

public sealed record UpdateAccountCommand(
    Guid Id,
    Guid CompanyId,
    string AccountName,
    bool IsActive,
    byte[] RowVersion) : ICommand<Result<AccountDto>>;
