using System;
using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.SystemBase.Companies;

public record UpdateCompanyCommand(
    Guid Id,
    DateOnly? FrozenAccountsDate,
    Guid? DefaultRetainedEarningsAccountId
) : ICommand<Result<CompanyDto>>;
