using System;

namespace Erp.Application.Features.SystemBase.Companies;

public class CompanyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly? FrozenAccountsDate { get; set; }
    public Guid? DefaultRetainedEarningsAccountId { get; set; }
    public string? DefaultRetainedEarningsAccountCode { get; set; }
}
