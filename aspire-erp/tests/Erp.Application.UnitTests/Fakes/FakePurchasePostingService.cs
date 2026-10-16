using Erp.Application.DTOs;
using Erp.Application.Services;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>
/// Strict <see cref="IPurchasePostingService"/> double: every call is recorded and then throws.
/// A governance-guard test therefore proves the rejection happened BEFORE any posting work -
/// if the guard were missing, the handler would reach this service and blow up.
/// </summary>
public sealed class FakePurchasePostingService : IPurchasePostingService
{
    public bool PostInvoiceCalled { get; private set; }
    public bool PostReceiptCalled { get; private set; }
    public bool PostReturnCalled { get; private set; }

    public Task<PurchaseReceiptPostingDto> PostReceiptAsync(
        PurchaseReceiptPostingRequest request,
        CancellationToken cancellationToken = default)
    {
        PostReceiptCalled = true;
        throw new InvalidOperationException("PostReceiptAsync should not have been called.");
    }

    public Task<PurchaseInvoicePostingDto> PostInvoiceAsync(
        PurchaseInvoicePostingRequest request,
        CancellationToken cancellationToken = default)
    {
        PostInvoiceCalled = true;
        throw new InvalidOperationException("PostInvoiceAsync should not have been called.");
    }

    public Task<PurchaseInvoicePostingDto> PostReturnAsync(
        PurchaseReturnPostingRequest request,
        CancellationToken cancellationToken = default)
    {
        PostReturnCalled = true;
        throw new InvalidOperationException("PostReturnAsync should not have been called.");
    }
}
