using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Features.Payments.Commands;

/// <summary>
/// Executes <see cref="CancelPaymentEntryCommand"/>: Submitted → Cancelled plus the
/// compensating reversal append and the invoice restoration, inside ONE transaction
/// (spec R-12 PE-05 / Constitution III.3).
/// </summary>
/// <remarks>
/// <para><b>Reversal posting date.</b> The reversal rows carry the voucher's ORIGINAL
/// PaymentDate, so <c>Company.EnsurePostingDateUnlocked</c> is checked against that date:
/// a frozen original period blocks the CANCELLATION too (the JournalEntry cancel precedent,
/// spec AC-04 wording: "posting, modification, or cancellation is blocked").</para>
/// <para><b>No counterparty postability re-check.</b> The accounts were vetted at submit and
/// the FK constraints guarantee they still exist; deactivating an account must never make a
/// voucher un-cancellable. Only the account rows (for the currency snapshot) are re-read.</para>
/// </remarks>
public sealed class CancelPaymentEntryCommandHandler : ICommandHandler<CancelPaymentEntryCommand, Result<PaymentEntryDto>>
{
    private readonly IBankRepository _banks;
    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly ICustomerRepository _customers;
    private readonly ISupplierRepository _suppliers;
    private readonly ISalesInvoiceRepository _salesInvoices;
    private readonly IPurchaseRepository _purchases;

    public CancelPaymentEntryCommandHandler(
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
        CancelPaymentEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // One transaction for status + restoration + reversal rows: the voucher ends up
            // Cancelled with a complete mirror image and settled invoices restored, or untouched.
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
                // R-13 FC-04: cancelling into a closed year is refused — the close is immutable.
                await _companies.EnsurePostingDateInOpenYearAsync(company.Id, payment.PaymentDate, token);

                // PE-05: only a Submitted, un-reconciled voucher cancels (Draft/double-cancel → 409).
                payment.Cancel();

                var bankProfile = await _banks.GetAccountByIdAsync(payment.BankAccountId, token)
                    ?? throw new BankingValidationException(
                        BankingErrorCodes.BankAccountNotFound,
                        $"Bank account '{payment.BankAccountId}' was not found in this company.");

                var bankGl = await _accounts.GetByIdAsync(bankProfile.GLAccountId, token)
                    ?? throw new BankingValidationException(
                        BankingErrorCodes.BankGlAccountNotFound,
                        $"Bank GL account '{bankProfile.GLAccountId}' was not found in this tenant.");

                // The counterparty leaf is re-resolved from live rows for the currency snapshot;
                // the submit-time postability is NOT re-gated (see remarks).
                var counterparty = await ResolveCounterpartyForReversalAsync(payment, token);

                var reversalLines = PaymentPosting.BuildLedgerLines(payment, bankGl, counterparty, isReversal: true);

                // Constitution III.1 on the rows that are about to be written (a mirror of a
                // balanced voucher is balanced, but the guard proves it rather than assuming it).
                DoubleEntryGuard.EnsureBalanced(reversalLines);

                var salesTotals = await RestoreSalesAllocationsAsync(payment, token);
                var purchaseTotals = await RestorePurchaseAllocationsAsync(payment, token);

                // FX: Realized Gain/Loss Plug Lines Reversal
                var totalFxDiff = 0m;
                if (payment.PartyType == PaymentPartyType.Customer)
                {
                    foreach (var (invId, data) in salesTotals)
                    {
                        totalFxDiff += FxCalculator.RealizedPerSlice(data.Total, payment.SettlementExchangeRate, data.InvoiceRate);
                    }
                }
                else
                {
                    foreach (var (invId, data) in purchaseTotals)
                    {
                        totalFxDiff += payment.PaymentType == PaymentType.Receive
                            ? FxCalculator.RealizedPerSlice(data.Total, payment.SettlementExchangeRate, data.InvoiceRate)
                            : -FxCalculator.RealizedPerSlice(data.Total, payment.SettlementExchangeRate, data.InvoiceRate);
                    }
                }

                if (totalFxDiff != 0m)
                {
                    if (string.IsNullOrWhiteSpace(company.DefaultExchangeGainLossAccountCode))
                    {
                        throw new BankingValidationException("invalid_exchange_gain_loss_account", "Company DefaultExchangeGainLossAccountCode is not configured.");
                    }
                    var fxAccounts = await _accounts.FindActiveLeafByCodeAsync(company.Id, company.DefaultExchangeGainLossAccountCode, token);
                    if (fxAccounts.Count != 1)
                    {
                        throw new BankingValidationException("invalid_exchange_gain_loss_account", "Company DefaultExchangeGainLossAccountCode does not resolve to exactly one active leaf.");
                    }
                    var fxAccount = fxAccounts[0];

                    // Original logic: totalFxDiff < 0 (Loss) -> Dr, totalFxDiff > 0 (Gain) -> Cr.
                    // For reversal: Loss -> Cr, Gain -> Dr.
                    var fxLine = new GLEntry
                    {
                        CompanyId = payment.CompanyId,
                        PostingDate = payment.PaymentDate,
                        AccountId = fxAccount.Id,
                        Account = fxAccount,
                        Debit = totalFxDiff > 0 ? totalFxDiff : 0m,
                        Credit = totalFxDiff < 0 ? -totalFxDiff : 0m,
                        DebitInAccountCurrency = totalFxDiff > 0 ? totalFxDiff : 0m,
                        CreditInAccountCurrency = totalFxDiff < 0 ? -totalFxDiff : 0m,
                        AccountCurrency = company.Currency?.Code ?? "USD",
                        VoucherType = PaymentPosting.VoucherType,
                        VoucherNo = payment.VoucherNo,
                        VoucherId = payment.Id,
                        PartyType = null,
                        PartyId = null,
                        CostCenterId = null,
                        IsCancelled = true,
                        Remarks = $"Reversal of {PaymentPosting.VoucherType} {payment.VoucherNo} (cancelled)"
                    };
                    reversalLines.Add(fxLine);
                }
                
                // Re-assert balance
                DoubleEntryGuard.EnsureBalanced(reversalLines);

                await _banks.UpdatePaymentAsync(payment, token);
                await _banks.AddGlEntriesAsync(reversalLines, token);

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
    /// Re-resolves the counterparty leaf for the reversal currency snapshot. Falls back to the
    /// submit-time... no: when neither the party default nor the company code resolves (masters
    /// changed since submit), the bank GL row itself carries the snapshot - a reversal must
    /// never fail for a display-only field.
    /// </summary>
    private async Task<Account> ResolveCounterpartyForReversalAsync(
        PaymentEntry payment,
        CancellationToken cancellationToken)
    {
        try
        {
            var company = await _companies.GetByIdAsync(payment.CompanyId, cancellationToken);
            if (company is null)
            {
                throw new BankingValidationException(
                    BankingErrorCodes.CompanyNotFound, "Company not found.");
            }

            if (payment.PartyType == PaymentPartyType.Customer)
            {
                var customer = await _customers.GetByIdAsync(payment.PartyId, cancellationToken);
                return await PaymentPosting.ResolveCounterpartyLeafAsync(
                    _accounts, payment.CompanyId,
                    customer?.DefaultReceivableAccountId,
                    company.DefaultReceivableAccountCode,
                    "receivable", cancellationToken);
            }

            var supplier = await _suppliers.GetByIdAsync(payment.PartyId, cancellationToken);
            return await PaymentPosting.ResolveCounterpartyLeafAsync(
                _accounts, payment.CompanyId,
                supplier?.DefaultPayableAccountId,
                company.AccountsPayableAccountCode,
                "payable", cancellationToken);
        }
        catch (BankingValidationException)
        {
            // Display-only fallback (see remarks): reuse the bank GL account so the currency
            // snapshot still resolves; amounts/directions are unaffected.
            var bankProfile = await _banks.GetAccountByIdAsync(payment.BankAccountId, cancellationToken);
            return await _accounts.GetByIdAsync(bankProfile!.GLAccountId, cancellationToken)
                ?? throw new BankingValidationException(
                    BankingErrorCodes.BankGlAccountNotFound,
                    $"Bank GL account '{bankProfile!.GLAccountId}' was not found in this tenant.");
        }
    }

    /// <summary>
    /// Restores every Receive-leg invoice: outstanding and paid move back by the voucher totals
    /// and the status is recomputed (PE-05: Paid → back to PartiallyPaid/Unpaid).
    /// </summary>
    private async Task<Dictionary<Guid, (decimal Total, decimal InvoiceRate)>> RestoreSalesAllocationsAsync(
        PaymentEntry payment,
        CancellationToken cancellationToken)
    {
        var totals = new Dictionary<Guid, (decimal Total, decimal InvoiceRate)>();
        var sums = new Dictionary<Guid, decimal>();
        foreach (var slice in payment.Allocations.Where(a => a.SalesInvoiceId.HasValue))
        {
            sums.TryGetValue(slice.SalesInvoiceId!.Value, out var running);
            sums[slice.SalesInvoiceId!.Value] = running + slice.AllocatedAmount;
        }

        foreach (var (invoiceId, total) in sums)
        {
            var invoice = await _salesInvoices.GetByIdAsync(invoiceId, cancellationToken)
                ?? throw new BankingValidationException(
                    BankingErrorCodes.PaymentPartyMismatch,
                    $"Sales invoice '{invoiceId}' was not found in this tenant.");

            invoice.OutstandingAmount += total;
            invoice.PaidAmount -= total;
            invoice.Status = invoice.OutstandingAmount <= 0m
                ? SalesInvoiceStatus.Paid
                : invoice.OutstandingAmount >= invoice.GrandTotal
                    ? SalesInvoiceStatus.Unpaid
                    : SalesInvoiceStatus.PartiallyPaid;

            await _salesInvoices.UpdateAsync(invoice, cancellationToken);
            totals[invoice.Id] = (total, invoice.ExchangeRate);
        }
        return totals;
    }

    /// <summary>Pay-leg mirror of <see cref="RestoreSalesAllocationsAsync"/>.</summary>
    private async Task<Dictionary<Guid, (decimal Total, decimal InvoiceRate)>> RestorePurchaseAllocationsAsync(
        PaymentEntry payment,
        CancellationToken cancellationToken)
    {
        var totals = new Dictionary<Guid, (decimal Total, decimal InvoiceRate)>();
        var sums = new Dictionary<Guid, decimal>();
        foreach (var slice in payment.Allocations.Where(a => a.PurchaseInvoiceId.HasValue))
        {
            sums.TryGetValue(slice.PurchaseInvoiceId!.Value, out var running);
            sums[slice.PurchaseInvoiceId!.Value] = running + slice.AllocatedAmount;
        }

        foreach (var (invoiceId, total) in sums)
        {
            var bill = await _purchases.GetInvoiceByIdAsync(invoiceId, cancellationToken)
                ?? throw new BankingValidationException(
                    BankingErrorCodes.PaymentPartyMismatch,
                    $"Purchase invoice '{invoiceId}' was not found in this tenant.");

            bill.OutstandingAmount += total;
            bill.Status = bill.OutstandingAmount <= 0m
                ? PurchaseInvoiceStatus.Paid
                : bill.OutstandingAmount >= bill.GrandTotal
                    ? PurchaseInvoiceStatus.Unpaid
                    : PurchaseInvoiceStatus.PartiallyPaid;

            await _purchases.UpdateInvoiceAsync(bill, cancellationToken);
            totals[bill.Id] = (total, bill.ExchangeRate);
        }
        return totals;
    }
}
