using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Features.Payments.Commands;

/// <summary>
/// Executes <see cref="SubmitPaymentEntryCommand"/>: Draft → Submitted plus the GL settlement
/// append and the invoice settlement, inside ONE transaction. Validation order is deliberate -
/// every gate runs BEFORE the first row is built, so each rejection leaves ZERO ledger rows
/// (the JournalEntry submit precedent):
/// <list type="number">
/// <item>existence + company ownership (<c>payment_not_found</c>);</item>
/// <item>client RowVersion compare-and-swap (<c>concurrency_conflict</c>);</item>
/// <item>period freeze on the payment date (spec AC-04);</item>
/// <item>the aggregate state machine <c>PaymentEntry.Submit()</c> (Draft-only);</item>
/// <item>bank profile active + company-owned, counterparty leaf resolution;</item>
/// <item>PE-02 revalidation of EVERY slice against the FRESH invoice outstanding (PE-07: the
/// invoice RowVersion race surfaces here as <c>concurrency_conflict</c> via the repositories,
/// an over-consumed balance as <c>over_allocation</c>);</item>
/// <item>gapless <c>PAY-YYYY-NNNNN</c> numbering (Constitution III.4) and the balanced GL pair
/// (PE-01, <c>DoubleEntryGuard</c>).</item>
/// </list>
/// </summary>
public sealed class SubmitPaymentEntryCommandHandler : ICommandHandler<SubmitPaymentEntryCommand, Result<PaymentEntryDto>>
{
    private readonly IBankRepository _banks;
    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly ICustomerRepository _customers;
    private readonly ISupplierRepository _suppliers;
    private readonly ISalesInvoiceRepository _salesInvoices;
    private readonly IPurchaseRepository _purchases;

    public SubmitPaymentEntryCommandHandler(
        IBankRepository banks,
        ICompanyRepository companies,
        IAccountRepository accounts,
        ICustomerRepository customers,
        ISupplierRepository suppliers,
        ISalesInvoiceRepository salesInvoices,
        IPurchaseRepository purchases)
    {
        _banks = banks;
        _companies = companies;
        _accounts = accounts;
        _customers = customers;
        _suppliers = suppliers;
        _salesInvoices = salesInvoices;
        _purchases = purchases;
    }

    public async Task<Result<PaymentEntryDto>> HandleAsync(
        SubmitPaymentEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // One transaction for numbering + status + invoice settlement + rows: a rollback
            // consumes no voucher number and writes nothing (Constitution III.4, PE-04).
            return await _banks.ExecuteInTransactionAsync(async token =>
            {
                var payment = await _banks.GetPaymentByIdAsync(command.PaymentEntryId, token)
                    ?? throw new BankingValidationException(
                        BankingErrorCodes.PaymentNotFound,
                        $"Payment voucher '{command.PaymentEntryId}' was not found in this tenant.");

                if (payment.CompanyId != command.CompanyId)
                {
                    throw new BankingValidationException(
                        BankingErrorCodes.PaymentNotFound,
                        $"Payment voucher '{payment.VoucherNo}' does not belong to company "
                        + $"'{command.CompanyId}'.");
                }

                PaymentPosting.EnsureRowVersion(payment, command.RowVersion);

                var company = await _companies.GetByIdAsync(payment.CompanyId, token)
                    ?? throw new BankingValidationException(
                        BankingErrorCodes.CompanyNotFound,
                        $"Company '{payment.CompanyId}' was not found in this tenant.");

                company.EnsurePostingDateUnlocked(payment.PaymentDate);

                // PE-04: only now does the state machine move Draft -> Submitted.
                payment.Submit();

                var bankProfile = await _banks.GetAccountByIdAsync(payment.BankAccountId, token);
                if (bankProfile is null || bankProfile.CompanyId != payment.CompanyId)
                {
                    throw new BankingValidationException(
                        BankingErrorCodes.BankAccountNotFound,
                        $"Bank account '{payment.BankAccountId}' was not found in this company.");
                }

                if (!bankProfile.IsActive)
                {
                    throw new BankingValidationException(
                        BankingErrorCodes.BankAccountNotFound,
                        $"Bank account '{bankProfile.AccountNumber}' is inactive.");
                }

                // PE-07: re-read every referenced invoice FRESH inside the posting lock and
                // re-check the PE-02 cap against live outstanding (a concurrent payment that
                // consumed the balance lands here as over_allocation; a torn row read as
                // concurrency_conflict from the invoice repositories).
                var salesTotals = await SettleSalesAllocationsAsync(payment, token);
                var purchaseTotals = await SettlePurchaseAllocationsAsync(payment, token);

                // Counterparty leaf: party default first, company GL-code default second (D3).
                var counterpartyLeaf = payment.PartyType == PaymentPartyType.Customer
                    ? await ResolveCustomerLeafAsync(payment, company, token)
                    : await ResolveSupplierLeafAsync(payment, company, token);

                var (bankGl, counterparty) = await PaymentPosting.LoadSettlementAccountsAsync(
                    _accounts, bankProfile, counterpartyLeaf, token);

                // PE-04: the fiscal number is born inside the numbering lock, after every gate.
                payment.VoucherNo = await _banks.NextPaymentVoucherNumberAsync(
                    payment.CompanyId, payment.PaymentDate.Year, token);

                var glLines = PaymentPosting.BuildLedgerLines(payment, bankGl, counterparty, isReversal: false);

                // Constitution III.1 on the rows that are about to be written.
                DoubleEntryGuard.EnsureBalanced(glLines);

                await _banks.UpdatePaymentAsync(payment, token);
                await _banks.AddGlEntriesAsync(glLines, token);

                return Result<PaymentEntryDto>.Success(PaymentEntryDto.From(payment));
            }, cancellationToken);
        }
        catch (BankingValidationException ex)
        {
            return Result<PaymentEntryDto>.Failure(ex.Code, ex.Message);
        }
        catch (DoubleEntryImbalanceException ex)
        {
            return Result<PaymentEntryDto>.Failure(ex.Code, ex.Message);
        }
        catch (FiscalPeriodLockedException ex)
        {
            return Result<PaymentEntryDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<PaymentEntryDto>.Failure(ex.Code, ex.Message);
        }
    }

    /// <summary>
    /// Settles the Receive-leg slices: revalidates each invoice (company, party, open status,
    /// PE-02 cap against fresh outstanding), then moves outstanding → paid and recomputes the
    /// invoice status. Returns per-invoice totals (unused by the caller - kept for symmetry
    /// with the Pay leg and future partial-failure reporting).
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> SettleSalesAllocationsAsync(
        PaymentEntry payment,
        CancellationToken cancellationToken)
    {
        var totals = new Dictionary<Guid, decimal>();
        foreach (var slice in payment.Allocations.Where(a => a.SalesInvoiceId.HasValue))
        {
            totals.TryGetValue(slice.SalesInvoiceId!.Value, out var running);
            totals[slice.SalesInvoiceId!.Value] = running + slice.AllocatedAmount;
        }

        foreach (var (invoiceId, total) in totals)
        {
            var invoice = await _salesInvoices.GetByIdAsync(invoiceId, cancellationToken)
                ?? throw new BankingValidationException(
                    BankingErrorCodes.PaymentPartyMismatch,
                    $"Sales invoice '{invoiceId}' was not found in this tenant.");

            if (invoice.CompanyId != payment.CompanyId || invoice.CustomerId != payment.PartyId)
            {
                throw new BankingValidationException(
                    BankingErrorCodes.PaymentPartyMismatch,
                    $"Sales invoice '{invoice.InvoiceNumber}' does not belong to this company/party.");
            }

            if (invoice.Status != SalesInvoiceStatus.Unpaid && invoice.Status != SalesInvoiceStatus.PartiallyPaid)
            {
                throw new BankingValidationException(
                    BankingErrorCodes.PaymentPartyMismatch,
                    $"Sales invoice '{invoice.InvoiceNumber}' is not open for allocation.");
            }

            // PE-02 against the LIVE balance (PE-07): the slices of THIS voucher never touched
            // the outstanding yet, so the voucher total must fit the current remainder.
            payment.Allocate(total, invoice.OutstandingAmount, 0m);

            invoice.OutstandingAmount -= total;
            invoice.PaidAmount += total;
            invoice.Status = RecomputeSalesStatus(invoice);

            await _salesInvoices.UpdateAsync(invoice, cancellationToken);
        }

        return totals;
    }

    /// <summary>Pay-leg mirror of <see cref="SettleSalesAllocationsAsync"/> (no PaidAmount column).</summary>
    private async Task<Dictionary<Guid, decimal>> SettlePurchaseAllocationsAsync(
        PaymentEntry payment,
        CancellationToken cancellationToken)
    {
        var totals = new Dictionary<Guid, decimal>();
        foreach (var slice in payment.Allocations.Where(a => a.PurchaseInvoiceId.HasValue))
        {
            totals.TryGetValue(slice.PurchaseInvoiceId!.Value, out var running);
            totals[slice.PurchaseInvoiceId!.Value] = running + slice.AllocatedAmount;
        }

        foreach (var (invoiceId, total) in totals)
        {
            var bill = await _purchases.GetInvoiceByIdAsync(invoiceId, cancellationToken)
                ?? throw new BankingValidationException(
                    BankingErrorCodes.PaymentPartyMismatch,
                    $"Purchase invoice '{invoiceId}' was not found in this tenant.");

            if (bill.CompanyId != payment.CompanyId || bill.SupplierId != payment.PartyId)
            {
                throw new BankingValidationException(
                    BankingErrorCodes.PaymentPartyMismatch,
                    $"Purchase invoice '{bill.BillNumber}' does not belong to this company/party.");
            }

            if (bill.Status != PurchaseInvoiceStatus.Unpaid && bill.Status != PurchaseInvoiceStatus.PartiallyPaid)
            {
                throw new BankingValidationException(
                    BankingErrorCodes.PaymentPartyMismatch,
                    $"Purchase invoice '{bill.BillNumber}' is not open for allocation.");
            }

            payment.Allocate(total, bill.OutstandingAmount, 0m);

            bill.OutstandingAmount -= total;
            bill.Status = RecomputePurchaseStatus(bill);

            await _purchases.UpdateInvoiceAsync(bill, cancellationToken);
        }

        return totals;
    }

    private static SalesInvoiceStatus RecomputeSalesStatus(SalesInvoice invoice)
        => invoice.OutstandingAmount <= 0m
            ? SalesInvoiceStatus.Paid
            : invoice.OutstandingAmount >= invoice.GrandTotal
                ? SalesInvoiceStatus.Unpaid
                : SalesInvoiceStatus.PartiallyPaid;

    private static PurchaseInvoiceStatus RecomputePurchaseStatus(PurchaseInvoice bill)
        => bill.OutstandingAmount <= 0m
            ? PurchaseInvoiceStatus.Paid
            : bill.OutstandingAmount >= bill.GrandTotal
                ? PurchaseInvoiceStatus.Unpaid
                : PurchaseInvoiceStatus.PartiallyPaid;

    private async Task<Account> ResolveCustomerLeafAsync(
        PaymentEntry payment, Company company, CancellationToken cancellationToken)
    {
        var customer = await _customers.GetByIdAsync(payment.PartyId, cancellationToken);
        return await PaymentPosting.ResolveCounterpartyLeafAsync(
            _accounts,
            payment.CompanyId,
            customer?.DefaultReceivableAccountId,
            company.DefaultReceivableAccountCode,
            "receivable",
            cancellationToken);
    }

    private async Task<Account> ResolveSupplierLeafAsync(
        PaymentEntry payment, Company company, CancellationToken cancellationToken)
    {
        var supplier = await _suppliers.GetByIdAsync(payment.PartyId, cancellationToken);
        return await PaymentPosting.ResolveCounterpartyLeafAsync(
            _accounts,
            payment.CompanyId,
            supplier?.DefaultPayableAccountId,
            company.AccountsPayableAccountCode,
            "payable",
            cancellationToken);
    }
}
