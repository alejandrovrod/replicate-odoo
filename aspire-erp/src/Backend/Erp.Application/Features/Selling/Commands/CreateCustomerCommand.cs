using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Selling.Commands;

/// <summary>
/// Creates one Customer (Task 5.1). Codes are unique per COMPANY (plan.md §1
/// UQ_Customer_Tenant_Company_Code) and duplicates are reported as a 409 domain failure through
/// <see cref="Result{T}"/> - the same pattern as CreateAccountCommand/CreateSupplierCommand.
/// </summary>
/// <remarks>
/// Optional fields default to the plan.md §1 column defaults (CreditLimit 0.0000 = no credit
/// control, BypassCreditLimitCheck 0, PaymentTermsDays 30); TaxId is NOT
/// NULL without a server default, so an omitted value becomes the empty string rather than
/// failing the insert. CurrencyId is RM-09 (nullable FK, "USD" fallback at read time).
/// </remarks>
public sealed record CreateCustomerCommand(
    Guid CompanyId,
    string CustomerCode,
    string CustomerName,
    string TaxId = "",
    decimal CreditLimit = 0m,
    bool BypassCreditLimitCheck = false,
    Guid? CurrencyId = null,
    int PaymentTermsDays = 30,
    Guid? DefaultReceivableAccountId = null,
    bool IsActive = true,
    string CustomerType = "Company",
    string CustomerGroup = "",
    string Territory = "",
    string BillingAddress = "",
    string Phone = "",
    string Email = "",
    string ContactPerson = "",
    string Website = "",
    string PaymentTerms = "",
    string CustomerDetails = "") : ICommand<Result<CustomerDto>>;
