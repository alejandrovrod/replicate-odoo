using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;

namespace Erp.Application.Features.HrPayroll.Commands;

/// <summary>
/// Creates one salary component (Task 12.2): an earning or deduction pay element bound to a
/// leaf posting account in the Chart of Accounts. A rejected creation writes zero rows (the
/// guards throw before the first Add).
/// </summary>
public sealed record CreateSalaryComponentCommand(
    Guid CompanyId,
    string ComponentName,
    SalaryComponentType ComponentType,
    Guid DefaultGLAccountId,
    bool DependsOnPaymentDays = false,
    bool IsTaxApplicable = true) : ICommand<Result<SalaryComponentDto>>;
