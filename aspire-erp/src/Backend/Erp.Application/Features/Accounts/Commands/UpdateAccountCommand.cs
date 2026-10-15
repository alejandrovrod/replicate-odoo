using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Accounts.Commands;

/// <summary>
/// Updates one account: name / active flag always; parent reassignment and IsGroup conversion
/// only when explicitly requested. RootType, Type, AccountCode, CompanyId and TenantId are
/// immutable after creation (a cross-RootType move is rejected instead of cascading).
/// </summary>
/// <remarks>
/// Backward compatible: the new members are optional, so bodies sent before this change
/// (id, companyId, accountName, isActive, rowVersion) still bind. Reparenting is tri-state:
/// <c>ChangeParent = false</c> leaves the parent untouched, <c>true + ParentAccountId = null</c>
/// moves the account to the root, <c>true + value</c> reparents under that account.
/// </remarks>
public sealed record UpdateAccountCommand(
    Guid Id,
    Guid CompanyId,
    string AccountName,
    bool IsActive,
    byte[] RowVersion,
    Guid? ParentAccountId = null,
    bool ChangeParent = false,
    bool? IsGroup = null) : ICommand<Result<AccountDto>>;
