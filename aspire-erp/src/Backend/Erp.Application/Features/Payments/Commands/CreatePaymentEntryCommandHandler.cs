using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Payments.Commands;

/// <summary>
/// Executes <see cref="CreatePaymentEntryCommand"/>: every gate runs BEFORE the first row is
/// built, so each rejection leaves zero rows (the JournalEntry submit precedent):
/// company/bank/party existence + company match, PE-06 direction, per-line invoice checks
/// (existence, company, party match, Unpaid/PartiallyPaid status, PE-02 cap against the live
/// outstanding) and PE-03 conservation. Then the header + slices persist atomically.
/// </summary>
public sealed class CreatePaymentEntryCommandHandler : ICommandHandler<CreatePaymentEntryCommand, Result<PaymentEntryDto>>
{
    private readonly IBankRepository _banks;
    private readonly ICompanyRepository _companies;
    private readonly ICustomerRepository _customers;
    private readonly ISupplierRepository _suppliers;
    private readonly ISalesInvoiceRepository _salesInvoices;
    private readonly IPurchaseRepository _purchases;

    public CreatePaymentEntryCommandHandler(
        IBankRepository banks,
        ICompanyRepository companies,
        ICustomerRepository customers,
        ISupplierRepository suppliers,
        ISalesInvoiceRepository salesInvoices,
        IPurchaseRepository purchases)
    {
        _banks = banks;
        _companies = companies;
        _customers = customers;
        _suppliers = suppliers;
        _salesInvoices = salesInvoices;
        _purchases = purchases;
    }

    public async Task<Result<PaymentEntryDto>> HandleAsync(
        CreatePaymentEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Enum.IsDefined(command.PaymentType))
            {
                throw new BankingValidationException(
                    BankingErrorCodes.InvalidPaidAmount,
                    $"PaymentType must be Receive or Pay (was '{command.PaymentType}').");
            }

            if (!Enum.IsDefined(command.PartyType))
            {
                throw new BankingValidationException(
                    BankingErrorCodes.PaymentPartyMismatch,
                    $"PartyType must be Customer or Supplier (was '{command.PartyType}').");
            }

            _ = await _companies.GetByIdAsync(command.CompanyId, cancellationToken)
                ?? throw new BankingValidationException(
                    BankingErrorCodes.CompanyNotFound,
                    $"Company '{command.CompanyId}' was not found in this tenant.");

            var bankAccount = await _banks.GetAccountByIdAsync(command.BankAccountId, cancellationToken);
            if (bankAccount is null || bankAccount.CompanyId != command.CompanyId)
            {
                throw new BankingValidationException(
                    BankingErrorCodes.BankAccountNotFound,
                    $"Bank account '{command.BankAccountId}' was not found in this company.");
            }

            if (!bankAccount.IsActive)
            {
                throw new BankingValidationException(
                    BankingErrorCodes.BankAccountNotFound,
                    $"Bank account '{bankAccount.AccountNumber}' is inactive.");
            }

            // PE-06 directional consistency, enforced before any row exists.
            var header = new PaymentEntry
            {
                PaymentType = command.PaymentType,
                PartyType = command.PartyType,
            };
            header.EnsureDirection();

            // The party must exist and belong to the company (PE-03 same-party rule root).
            if (command.PartyType == PaymentPartyType.Customer)
            {
                var customer = await _customers.GetByIdAsync(command.PartyId, cancellationToken);
                if (customer is null || customer.CompanyId != command.CompanyId)
                {
                    throw new BankingValidationException(
                        BankingErrorCodes.PaymentPartyMismatch,
                        $"Customer '{command.PartyId}' was not found in this company.");
                }
            }
            else
            {
                var supplier = await _suppliers.GetByIdAsync(command.PartyId, cancellationToken);
                if (supplier is null)
                {
                    throw new BankingValidationException(
                        BankingErrorCodes.PaymentPartyMismatch,
                        $"Supplier '{command.PartyId}' was not found in this tenant.");
                }
            }

            // Per-line invoice gates (existence, company, party, status, PE-02 cap).
            var slices = new List<(Guid? SalesId, Guid? PurchaseId, decimal Amount)>(command.Allocations.Count);
            foreach (var input in command.Allocations)
            {
                slices.Add(await ValidateAllocationAsync(command, header, input, slices, cancellationToken));
            }

            var payment = new PaymentEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = command.CompanyId,
                BankAccountId = command.BankAccountId,
                PaymentType = command.PaymentType,
                PartyType = command.PartyType,
                PartyId = command.PartyId,
                PaymentDate = command.PaymentDate,
                PaidAmount = command.PaidAmount,
                ReferenceNumber = string.IsNullOrWhiteSpace(command.ReferenceNumber)
                    ? null
                    : command.ReferenceNumber.Trim(),
                DocumentStatus = PaymentDocumentStatus.Draft,
                Status = PaymentStatus.Unreconciled,
                Allocations = slices.Select(s => new PaymentAllocation
                {
                    Id = Guid.NewGuid(),
                    SalesInvoiceId = s.SalesId,
                    PurchaseInvoiceId = s.PurchaseId,
                    AllocatedAmount = s.Amount,
                }).ToList(),
            };

            // PE-03 conservation over the validated slices; derives the advance remainder.
            var allocatedTotal = slices.Sum(s => s.Amount);
            payment.EnsureConservation(slices.Select(s => s.Amount), command.PaidAmount - allocatedTotal);
            payment.UnallocatedAmount = command.PaidAmount - allocatedTotal;

            await _banks.AddPaymentAsync(payment, cancellationToken);

            return Result<PaymentEntryDto>.Success(PaymentEntryDto.From(payment));
        }
        catch (BankingValidationException ex)
        {
            return Result<PaymentEntryDto>.Failure(ex.Code, ex.Message);
        }
    }

    /// <summary>
    /// Validates one allocation slice and returns its normalized (salesId, purchaseId, amount)
    /// triple. Exactly one invoice reference must be set, matching the voucher leg (PE-06);
    /// the invoice must be open (Unpaid/PartiallyPaid), company- and party-owned, and the
    /// running total for that invoice must respect the PE-02 cap.
    /// </summary>
    private async Task<(Guid? SalesId, Guid? PurchaseId, decimal Amount)> ValidateAllocationAsync(
        CreatePaymentEntryCommand command,
        PaymentEntry header,
        PaymentAllocationInput input,
        IReadOnlyList<(Guid? SalesId, Guid? PurchaseId, decimal Amount)> accepted,
        CancellationToken cancellationToken)
    {
        var isReceive = command.PaymentType == PaymentType.Receive;

        if ((input.SalesInvoiceId.HasValue == input.PurchaseInvoiceId.HasValue))
        {
            throw new BankingValidationException(
                BankingErrorCodes.PaymentPartyMismatch,
                "Each allocation must reference exactly one invoice "
                + "(SalesInvoiceId xor PurchaseInvoiceId).");
        }

        if (isReceive && !input.SalesInvoiceId.HasValue)
        {
            throw new BankingValidationException(
                BankingErrorCodes.PaymentPartyMismatch,
                "A Receive voucher can only allocate to SalesInvoice rows.");
        }

        if (!isReceive && !input.PurchaseInvoiceId.HasValue)
        {
            throw new BankingValidationException(
                BankingErrorCodes.PaymentPartyMismatch,
                "A Pay voucher can only allocate to PurchaseInvoice rows.");
        }

        if (input.SalesInvoiceId.HasValue)
        {
            var invoice = await _salesInvoices.GetByIdAsync(input.SalesInvoiceId.Value, cancellationToken)
                ?? throw new BankingValidationException(
                    BankingErrorCodes.PaymentPartyMismatch,
                    $"Sales invoice '{input.SalesInvoiceId.Value}' was not found in this tenant.");

            EnsureOpenInvoice(
                invoice.CompanyId,
                invoice.CustomerId,
                invoice.Status != SalesInvoiceStatus.Unpaid && invoice.Status != SalesInvoiceStatus.PartiallyPaid,
                command,
                $"Sales invoice '{invoice.InvoiceNumber}'");

            var already = accepted.Where(s => s.SalesId == invoice.Id).Sum(s => s.Amount);
            header.Allocate(input.AllocatedAmount, invoice.OutstandingAmount, already);

            return (invoice.Id, null, input.AllocatedAmount);
        }

        {
            var bill = await _purchases.GetInvoiceByIdAsync(input.PurchaseInvoiceId!.Value, cancellationToken)
                ?? throw new BankingValidationException(
                    BankingErrorCodes.PaymentPartyMismatch,
                    $"Purchase invoice '{input.PurchaseInvoiceId.Value}' was not found in this tenant.");

            EnsureOpenInvoice(
                bill.CompanyId,
                bill.SupplierId,
                bill.Status != PurchaseInvoiceStatus.Unpaid && bill.Status != PurchaseInvoiceStatus.PartiallyPaid,
                command,
                $"Purchase invoice '{bill.BillNumber}'");

            var already = accepted.Where(s => s.PurchaseId == bill.Id).Sum(s => s.Amount);
            header.Allocate(input.AllocatedAmount, bill.OutstandingAmount, already);

            return (null, bill.Id, input.AllocatedAmount);
        }
    }

    /// <summary>
    /// Shared open-invoice gate: same company, same party, and an Unpaid/PartiallyPaid status.
    /// </summary>
    /// <exception cref="BankingValidationException">Any mismatch (all 400-domain failures).</exception>
    private static void EnsureOpenInvoice(
        Guid invoiceCompanyId,
        Guid invoicePartyId,
        bool closedStatus,
        CreatePaymentEntryCommand command,
        string label)
    {
        if (invoiceCompanyId != command.CompanyId || invoicePartyId != command.PartyId)
        {
            throw new BankingValidationException(
                BankingErrorCodes.PaymentPartyMismatch,
                $"{label} does not belong to this company/party.");
        }

        if (closedStatus)
        {
            throw new BankingValidationException(
                BankingErrorCodes.PaymentPartyMismatch,
                $"{label} is not open for allocation (Unpaid/PartiallyPaid required).");
        }
    }
}
