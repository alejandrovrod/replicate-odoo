using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Features.Selling.Commands;

public sealed class SubmitSalesInvoiceCommandHandler : ICommandHandler<SubmitSalesInvoiceCommand, Result<SalesInvoiceDto>>
{
    private readonly ISalesInvoiceRepository _salesInvoices;
    private readonly ICustomerRepository _customers;
    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;

    public SubmitSalesInvoiceCommandHandler(
        ISalesInvoiceRepository salesInvoices,
        ICustomerRepository customers,
        ICompanyRepository companies,
        IAccountRepository accounts)
    {
        _salesInvoices = salesInvoices;
        _customers = customers;
        _companies = companies;
        _accounts = accounts;
    }

    public async Task<Result<SalesInvoiceDto>> HandleAsync(SubmitSalesInvoiceCommand request, CancellationToken cancellationToken)
    {
        try
        {
            return await _salesInvoices.ExecuteInTransactionAsync(async token =>
            {
                var invoice = await _salesInvoices.GetByIdAsync(request.SalesInvoiceId, token);
                if (invoice is null) return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.ValidationFailed, "Invoice not found.");

                if (invoice.CompanyId != request.CompanyId)
                    return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.ValidationFailed, "Invoice company mismatch.");

                if (invoice.Status != SalesInvoiceStatus.Draft)
                    return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.ValidationFailed, "Invoice must be Draft to submit.");

                var company = await _companies.GetByIdAsync(request.CompanyId, token);
                if (company is null) return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.CompanyNotFound, "Company not found.");

                company.EnsurePostingDateUnlocked(invoice.PostingDate);

                var customer = await _customers.GetByIdAsync(invoice.CustomerId, token);
                if (customer is null) return Result<SalesInvoiceDto>.Failure(SellingErrorCodes.CustomerNotFound, "Customer not found.");

                // spec SL-02 / plan.md §2: credit gate
                CreditControlEvaluator.ValidateCreditExposure(customer, invoice.GrandTotal);

                // Update outstanding amount on customer
                customer.OutstandingAmount += invoice.GrandTotal;
                await _customers.UpdateAsync(customer, token);

                invoice.Status = SalesInvoiceStatus.Unpaid;
                invoice.OutstandingAmount = invoice.GrandTotal; // Just to be sure it matches
                await _salesInvoices.UpdateAsync(invoice, token);

                var glEntries = new List<GLEntry>();

                // Debit A/R
                Guid receivableAccountId = Guid.Empty;
                if (customer.DefaultReceivableAccountId.HasValue)
                {
                    receivableAccountId = customer.DefaultReceivableAccountId.Value;
                }
                else if (!string.IsNullOrEmpty(company.DefaultReceivableAccountCode))
                {
                    var accts = await _accounts.FindActiveLeafByCodeAsync(request.CompanyId, company.DefaultReceivableAccountCode, token);
                    if (accts.Count == 1) receivableAccountId = accts[0].Id;
                }

                glEntries.Add(new GLEntry
                {
                    TenantId = company.TenantId,
                    CompanyId = request.CompanyId,
                    PostingDate = invoice.PostingDate,
                    AccountId = receivableAccountId,
                    Debit = invoice.GrandTotal,
                    Credit = 0,
                    VoucherType = "Sales Invoice",
                    VoucherNo = invoice.InvoiceNumber,
                    VoucherId = invoice.Id,
                    PartyType = "Customer",
                    PartyId = customer.Id,
                    Remarks = "Sales Invoice Submission - A/R",
                    CreatedAt = DateTimeOffset.UtcNow
                });

                // Credit Sales Revenue
                Guid incomeAccountId = Guid.Empty;
                if (!string.IsNullOrEmpty(company.DefaultIncomeAccountCode))
                {
                    var accts = await _accounts.FindActiveLeafByCodeAsync(request.CompanyId, company.DefaultIncomeAccountCode, token);
                    if (accts.Count == 1) incomeAccountId = accts[0].Id;
                }

                glEntries.Add(new GLEntry
                {
                    TenantId = company.TenantId,
                    CompanyId = request.CompanyId,
                    PostingDate = invoice.PostingDate,
                    AccountId = incomeAccountId,
                    Debit = 0,
                    Credit = invoice.NetTotal,
                    VoucherType = "Sales Invoice",
                    VoucherNo = invoice.InvoiceNumber,
                    VoucherId = invoice.Id,
                    PartyType = "Customer",
                    PartyId = customer.Id,
                    Remarks = "Sales Invoice Submission - Revenue",
                    CreatedAt = DateTimeOffset.UtcNow
                });

                await _salesInvoices.AddGlEntriesAsync(glEntries, token);

                var dto = new SalesInvoiceDto(
                    invoice.Id, invoice.CompanyId, invoice.InvoiceNumber, invoice.CustomerId,
                    customer.CustomerName, invoice.PostingDate, invoice.DueDate, invoice.Status,
                    invoice.IsPOS, invoice.UpdateStock, invoice.SourceWarehouseId,
                    invoice.NetTotal, invoice.TaxTotal, invoice.GrandTotal,
                    invoice.OutstandingAmount, invoice.PaidAmount,
                    Convert.ToBase64String(invoice.RowVersion ?? Array.Empty<byte>()),
                    invoice.CreatedAt,
                    invoice.Items.Select(i => new SalesInvoiceItemDto(i.Id, i.ItemId, i.SalesOrderItemId, i.Quantity, i.Rate, i.Amount)).ToList()
                );

                return Result<SalesInvoiceDto>.Success(dto);

            }, cancellationToken);
        }
        catch (CreditLimitExceededException ex)
        {
            return Result<SalesInvoiceDto>.Failure(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            return Result<SalesInvoiceDto>.Failure("server_error", ex.Message);
        }
    }
}
