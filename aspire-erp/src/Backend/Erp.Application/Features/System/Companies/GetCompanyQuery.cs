using System;
using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.SystemBase.Companies;

public record GetCompanyQuery(Guid Id) : IQuery<Result<CompanyDto>>;
